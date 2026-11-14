using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Time;

namespace NexGrades.Domain.Services;

/// <summary>
/// Creates, edits, voids and restores assessments: the graded occasions (a class test, an oral check) that are
/// the columns of the grade sheet.
/// </summary>
/// <param name="contextFactory">Creates the short-lived context that each call opens.</param>
/// <param name="clock">Source of the timestamp that voiding an assessment records.</param>
/// <remarks>
/// Opens a short-lived context per call; see <see cref="EnrollmentService"/> for why. Writes to closed years
/// are rejected and audited by the closed-year interceptor. Assessments are never deleted: voiding sets
/// <see cref="Assessment.VoidedOn"/> and can be undone. A voided assessment and its grades count toward no
/// average, and the assessment cannot be edited or graded until it is restored.
/// </remarks>
public sealed class AssessmentService(IDbContextFactory<AppDbContext> contextFactory, IClock clock)
{
    /// <summary>Maximum length of an assessment's title, after trimming.</summary>
    public const int MaxTitleLength = 100;

    /// <summary>Creates an assessment in <paramref name="gradeBucketId"/> on <paramref name="date"/>.</summary>
    /// <exception cref="InvariantViolationException">
    /// The bucket does not exist, the title is empty or too long, or the date is outside the school year.
    /// </exception>
    /// <exception cref="ClosedYearException">The year is closed and not unlocked.</exception>
    public Task<Assessment> CreateAssessmentAsync(
        int gradeBucketId, string title, DateOnly date, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("AssessmentService.CreateAssessment", async () =>
        {
            var normalizedTitle = RequiredText.Normalize(title, "An assessment title", MaxTitleLength);

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var bucket = await context.GradeBuckets.AsNoTracking()
                .Where(b => b.Id == gradeBucketId)
                .Select(b => new { b.Id, b.SubjectInstance.ClassInstance.SchoolYear })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvariantViolationException($"Grade bucket {gradeBucketId} does not exist.");
            EnsureDateInYear(date, bucket.SchoolYear);

            var assessment = new Assessment { GradeBucketId = gradeBucketId, Title = normalizedTitle, Date = date };
            context.Assessments.Add(assessment);
            await context.SaveChangesAsync(cancellationToken);

            return assessment;
        });

    /// <summary>
    /// Changes an assessment's title and date; its bucket cannot change. When the date changes, the active
    /// grades that still carry the old date move to the new one, in the same save. Grades whose date was edited
    /// by hand keep it.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// The assessment does not exist or is voided, the title is empty or too long, the date is outside the
    /// school year, or a student whose grade would move was not enrolled or is exempt on the new date (their ids
    /// are listed in the message and nothing is changed).
    /// </exception>
    /// <exception cref="ClosedYearException">The year is closed and not unlocked.</exception>
    public Task<Assessment> EditAssessmentAsync(
        int assessmentId, string title, DateOnly date, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("AssessmentService.EditAssessment", async () =>
        {
            var normalizedTitle = RequiredText.Normalize(title, "An assessment title", MaxTitleLength);

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var assessment = await LoadAsync(context, assessmentId, cancellationToken);
            if (assessment.VoidedOn is not null)
            {
                throw new InvariantViolationException($"Assessment {assessmentId} is voided and cannot be edited; restore it first.");
            }

            var subjectInstance = assessment.GradeBucket.SubjectInstance;
            EnsureDateInYear(date, subjectInstance.ClassInstance.SchoolYear);

            if (date != assessment.Date)
            {
                await MoveGradesAsync(context, assessment, subjectInstance, date, cancellationToken);
            }

            assessment.Title = normalizedTitle;
            assessment.Date = date;
            await context.SaveChangesAsync(cancellationToken);

            return assessment;
        });

    /// <summary>
    /// Voids an assessment: it and its grades leave the grade sheet, the averages and the export. Nothing is
    /// deleted and the grades' own voided state is not touched.
    /// </summary>
    /// <exception cref="InvariantViolationException">The assessment does not exist or is already voided.</exception>
    /// <exception cref="ClosedYearException">The year is closed and not unlocked.</exception>
    public Task VoidAssessmentAsync(int assessmentId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("AssessmentService.VoidAssessment", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var assessment = await context.Assessments.FirstOrDefaultAsync(a => a.Id == assessmentId, cancellationToken)
                ?? throw new InvariantViolationException($"Assessment {assessmentId} does not exist.");
            if (assessment.VoidedOn is not null)
            {
                throw new InvariantViolationException($"Assessment {assessmentId} is already voided.");
            }

            assessment.VoidedOn = clock.Now;
            await context.SaveChangesAsync(cancellationToken);
        });

    /// <summary>Restores a voided assessment together with its grades that are not voided themselves.</summary>
    /// <exception cref="InvariantViolationException">The assessment does not exist or is not voided.</exception>
    /// <exception cref="ClosedYearException">The year is closed and not unlocked.</exception>
    public Task RestoreAssessmentAsync(int assessmentId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("AssessmentService.RestoreAssessment", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var assessment = await context.Assessments.FirstOrDefaultAsync(a => a.Id == assessmentId, cancellationToken)
                ?? throw new InvariantViolationException($"Assessment {assessmentId} does not exist.");
            if (assessment.VoidedOn is null)
            {
                throw new InvariantViolationException($"Assessment {assessmentId} is not voided.");
            }

            assessment.VoidedOn = null;
            await context.SaveChangesAsync(cancellationToken);
        });

    private static async Task<Assessment> LoadAsync(AppDbContext context, int assessmentId, CancellationToken cancellationToken) =>
        await context.Assessments
            .Include(a => a.GradeBucket).ThenInclude(b => b.SubjectInstance).ThenInclude(si => si.ClassInstance).ThenInclude(ci => ci.SchoolYear)
            .FirstOrDefaultAsync(a => a.Id == assessmentId, cancellationToken)
        ?? throw new InvariantViolationException($"Assessment {assessmentId} does not exist.");

    private static void EnsureDateInYear(DateOnly date, SchoolYear schoolYear)
    {
        var asDateTime = date.ToDateTime(TimeOnly.MinValue);
        if (asDateTime < schoolYear.StartDate.Date || asDateTime > schoolYear.EndDate.Date)
        {
            throw new InvariantViolationException(
                $"{date:O} is outside school year {schoolYear.Id} ({DateOnly.FromDateTime(schoolYear.StartDate):O} to {DateOnly.FromDateTime(schoolYear.EndDate):O}).");
        }
    }

    /// <summary>
    /// Moves the active grades that carry the assessment's old date to <paramref name="newDate"/>, after
    /// checking that every affected student is still gradable on it. Throws before changing anything.
    /// </summary>
    private static async Task MoveGradesAsync(
        AppDbContext context, Assessment assessment, SubjectInstance subjectInstance, DateOnly newDate, CancellationToken cancellationToken)
    {
        var grades = await context.Grades
            .Where(g => g.AssessmentId == assessment.Id && g.VoidedOn == null && g.RecordedOn == assessment.Date)
            .ToListAsync(cancellationToken);
        if (grades.Count == 0)
        {
            return;
        }

        var studentIds = grades.Select(g => g.StudentId).ToList();
        var enrollments = await context.Enrollments.AsNoTracking()
            .Where(e => e.ClassInstanceId == subjectInstance.ClassInstanceId && studentIds.Contains(e.StudentId))
            .ToListAsync(cancellationToken);
        var exemptIds = (await context.SubjectParticipations.AsNoTracking()
            .Where(p => p.SubjectInstanceId == subjectInstance.Id && p.Mode == ParticipationMode.Exempt && studentIds.Contains(p.StudentId))
            .Select(p => p.StudentId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var newDateTime = newDate.ToDateTime(TimeOnly.MinValue);
        var blocked = studentIds
            .Where(id => exemptIds.Contains(id)
                || !enrollments.Any(e => e.StudentId == id && EnrollmentActivity.IsActive(e, newDateTime)))
            .Distinct()
            .Order()
            .ToList();
        if (blocked.Count > 0)
        {
            throw new InvariantViolationException(
                $"Assessment {assessment.Id} cannot move to {newDate:O}: students {string.Join(", ", blocked)} were not enrolled or are exempt on that date.");
        }

        foreach (var grade in grades)
        {
            grade.RecordedOn = newDate;
        }
    }
}
