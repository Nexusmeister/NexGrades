using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Time;

namespace NexGrades.Domain.Services;

/// <summary>
/// Implements the year close workflow: reports readiness (blocking issues and advisory warnings), then, once
/// nothing blocks it, flips the year to <see cref="SchoolYearStatus.Closed"/>. Closing the Active year goes
/// through <see cref="CloseAndActivateNextYearAsync"/>, which activates the next year in the same transaction,
/// so the app never ends up with no Active year.
/// </summary>
/// <param name="contextFactory">Creates the short-lived context that each call opens.</param>
/// <param name="clock">Source of "now", used for the closing timestamp and for active enrollments.</param>
/// <param name="logger">Receives a content-free line when the active year changes.</param>
/// <remarks>Opens a short-lived context per call; see <see cref="EnrollmentService"/> for why.</remarks>
public sealed partial class YearCloseService(IDbContextFactory<AppDbContext> contextFactory, IClock clock, ILogger<YearCloseService> logger)
{
    /// <summary>
    /// Reports what would prevent <paramref name="schoolYearId"/> from closing right now (blocking), and
    /// what looks unfinished but would not prevent the close (advisory).
    /// </summary>
    public Task<CloseReadiness> GetCloseReadinessAsync(int schoolYearId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("YearCloseService.GetCloseReadiness", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await GetCloseReadinessAsync(context, schoolYearId, cancellationToken);
        });

    /// <summary>
    /// Readiness check on a caller-supplied context, so the close operations check and close
    /// within the same unit of work.
    /// </summary>
    private async Task<CloseReadiness> GetCloseReadinessAsync(
        AppDbContext context, int schoolYearId, CancellationToken cancellationToken)
    {
        var schoolYearExists = await context.SchoolYears.AnyAsync(sy => sy.Id == schoolYearId, cancellationToken);
        if (!schoolYearExists)
        {
            throw new InvariantViolationException($"School year {schoolYearId} does not exist.");
        }

        var classInstances = await context.ClassInstances
            .Where(ci => ci.SchoolYearId == schoolYearId)
            .Include(ci => ci.Enrollments)
            .Include(ci => ci.SubjectInstances).ThenInclude(si => si.Subject)
            .Include(ci => ci.SubjectInstances).ThenInclude(si => si.GradeBuckets).ThenInclude(gb => gb.Grades)
            .Include(ci => ci.SubjectInstances).ThenInclude(si => si.SubjectParticipations)
            .ToListAsync(cancellationToken);

        var blockingIssues = new List<CloseReadinessIssue>();
        var warnings = new List<CloseReadinessIssue>();

        foreach (var classInstance in classInstances)
        {
            if (classInstance.Status == ClassInstanceStatus.Active)
            {
                blockingIssues.Add(new CloseReadinessIssue(
                    CloseReadinessIssueKind.ClassStillActive, classInstance.Id, classInstance.Name));
            }

            var activeStudentIds = classInstance.Enrollments
                .Where(e => EnrollmentActivity.IsActive(e, clock))
                .Select(e => e.StudentId)
                .ToHashSet();

            AddSubjectInstanceWarnings(classInstance, activeStudentIds, warnings);
        }

        var studentIds = warnings.Where(w => w.StudentId is not null).Select(w => w.StudentId!.Value).Distinct().ToList();
        var students = await context.Students.AsNoTracking()
            .Where(s => studentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var namedWarnings = warnings
            .Select(w => w.StudentId is { } id && students.TryGetValue(id, out var student)
                ? w with { StudentFirstName = student.FirstName, StudentLastName = student.LastName }
                : w)
            .OrderBy(w => w.ClassName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(w => w.SubjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(w => w.StudentLastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(w => w.StudentFirstName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CloseReadiness(
            [.. blockingIssues.OrderBy(i => i.ClassName, StringComparer.OrdinalIgnoreCase)],
            namedWarnings);
    }

    /// <summary>
    /// Adds the advisory warnings for every subject of one class: no grades, bucket weights not totalling 100,
    /// and participating students without a grade.
    /// </summary>
    private static void AddSubjectInstanceWarnings(
        ClassInstance classInstance, HashSet<int> activeStudentIds, List<CloseReadinessIssue> warnings)
    {
        foreach (var subjectInstance in classInstance.SubjectInstances)
        {
            var subjectName = subjectInstance.Subject.Name;

            var totalGradeCount = subjectInstance.GradeBuckets.Sum(gb => gb.Grades.Count);
            if (totalGradeCount == 0)
            {
                warnings.Add(new CloseReadinessIssue(
                    CloseReadinessIssueKind.SubjectWithoutGrades, classInstance.Id, classInstance.Name,
                    subjectInstance.Id, subjectName));
            }

            var weightSum = subjectInstance.GradeBuckets.Sum(gb => gb.Weight);
            if (weightSum != 100m)
            {
                warnings.Add(new CloseReadinessIssue(
                    CloseReadinessIssueKind.BucketWeightsNotHundred, classInstance.Id, classInstance.Name,
                    subjectInstance.Id, subjectName, WeightTotal: weightSum));
            }

            var exemptStudentIds = subjectInstance.SubjectParticipations
                .Where(p => p.Mode == ParticipationMode.Exempt)
                .Select(p => p.StudentId)
                .ToHashSet();
            var explicitlyParticipatingStudentIds = subjectInstance.SubjectParticipations
                .Where(p => p.Mode == ParticipationMode.Participating)
                .Select(p => p.StudentId);

            var participatingStudentIds = activeStudentIds
                .Where(studentId => !exemptStudentIds.Contains(studentId))
                .Union(explicitlyParticipatingStudentIds);

            foreach (var studentId in participatingStudentIds)
            {
                var hasGrade = subjectInstance.GradeBuckets
                    .Any(gb => gb.Grades.Any(g => g.StudentId == studentId));
                if (!hasGrade)
                {
                    warnings.Add(new CloseReadinessIssue(
                        CloseReadinessIssueKind.StudentWithoutGrade, classInstance.Id, classInstance.Name,
                        subjectInstance.Id, subjectName, studentId));
                }
            }
        }
    }

    /// <summary>
    /// Closes <paramref name="schoolYearId"/>: sets <see cref="SchoolYear.Status"/> to
    /// <see cref="SchoolYearStatus.Closed"/> and <see cref="SchoolYear.ClosedOn"/> to now, but only once
    /// every blocking issue from <see cref="GetCloseReadinessAsync(int, CancellationToken)"/> has been resolved.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// The year is Active (use <see cref="CloseAndActivateNextYearAsync"/>), or blocking issues remain; their
    /// class ids are listed in the message.
    /// </exception>
    public Task CloseSchoolYearAsync(int schoolYearId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("YearCloseService.CloseSchoolYear", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var readiness = await GetCloseReadinessAsync(context, schoolYearId, cancellationToken);
            var schoolYear = await context.SchoolYears.FirstAsync(sy => sy.Id == schoolYearId, cancellationToken);
            if (schoolYear.Status == SchoolYearStatus.Active)
            {
                throw new InvariantViolationException(
                    $"School year {schoolYearId} is Active; close it with {nameof(CloseAndActivateNextYearAsync)} so that the next year becomes Active.");
            }

            EnsureCanClose(schoolYearId, readiness);

            schoolYear.Status = SchoolYearStatus.Closed;
            schoolYear.ClosedOn = clock.Now;

            await context.SaveChangesAsync(cancellationToken);
        });

    /// <summary>
    /// Closes the Active <paramref name="schoolYearId"/> and activates the Planned <paramref name="nextSchoolYearId"/>
    /// in one transaction, so a failure leaves both years unchanged and there is never a moment without an Active
    /// year.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// Either year is missing, the years are the same, the first is not Active, the second is not Planned or does
    /// not start after the first, or blocking issues remain (their class ids are listed in the message).
    /// </exception>
    public Task CloseAndActivateNextYearAsync(int schoolYearId, int nextSchoolYearId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("YearCloseService.CloseAndActivateNextYear", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            if (schoolYearId == nextSchoolYearId)
            {
                throw new InvariantViolationException($"School year {schoolYearId} cannot be its own next year.");
            }

            var old = await context.SchoolYears.FirstOrDefaultAsync(sy => sy.Id == schoolYearId, cancellationToken)
                ?? throw new InvariantViolationException($"School year {schoolYearId} does not exist.");
            var next = await context.SchoolYears.FirstOrDefaultAsync(sy => sy.Id == nextSchoolYearId, cancellationToken)
                ?? throw new InvariantViolationException($"School year {nextSchoolYearId} does not exist.");

            if (old.Status != SchoolYearStatus.Active)
            {
                throw new InvariantViolationException(
                    $"School year {schoolYearId} is not Active (current status: {old.Status}).");
            }

            if (next.Status != SchoolYearStatus.Planned)
            {
                throw new InvariantViolationException(
                    $"School year {nextSchoolYearId} is not Planned (current status: {next.Status}).");
            }

            if (next.StartDate <= old.StartDate)
            {
                throw new InvariantViolationException(
                    $"School year {nextSchoolYearId} does not start after school year {schoolYearId}.");
            }

            var readiness = await GetCloseReadinessAsync(context, schoolYearId, cancellationToken);
            EnsureCanClose(schoolYearId, readiness);

            old.Status = SchoolYearStatus.Closed;
            old.ClosedOn = clock.Now;
            next.Status = SchoolYearStatus.Active;

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            LogYearClosedAndNextActivated();
        });

    /// <summary>Throws when blocking issues remain. The message carries class ids only, never names.</summary>
    private static void EnsureCanClose(int schoolYearId, CloseReadiness readiness)
    {
        if (!readiness.CanClose)
        {
            throw new InvariantViolationException(
                $"Cannot close school year {schoolYearId}: classes still Active: " +
                string.Join(", ", readiness.BlockingIssues.Select(i => i.ClassInstanceId)) + ".");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Closed the active school year and activated the next one")]
    private partial void LogYearClosedAndNextActivated();
}
