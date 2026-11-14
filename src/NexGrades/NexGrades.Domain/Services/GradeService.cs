using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Grading;
using NexGrades.Domain.Time;

namespace NexGrades.Domain.Services;

/// <summary>
/// Records grades after validating that the student must be enrolled in the class instance that owns the
/// grade's bucket, and the student must be participating (not exempt) in that subject instance. Also sets and
/// clears the teacher's override of a student's final grade in a subject instance; the proposed final grade
/// itself is never stored (see <see cref="GradingScale.ProposeFinalGrade"/>).
/// </summary>
/// <param name="contextFactory">Creates the short-lived context that each call opens.</param>
/// <param name="clock">Source of the timestamp that voiding a grade records.</param>
/// <remarks>Opens a short-lived context per call; see <see cref="EnrollmentService"/> for why.</remarks>
public sealed class GradeService(IDbContextFactory<AppDbContext> contextFactory, IClock clock)
{
    /// <summary>Maximum length of a grade's label, after trimming.</summary>
    public const int MaxLabelLength = 100;

    /// <summary>Maximum length of a grade's comment, after trimming.</summary>
    public const int MaxCommentLength = 1000;

    /// <summary>
    /// Records a grade after validating that <paramref name="studentId"/> was enrolled in the class instance
    /// owning <paramref name="gradeBucketId"/>'s subject instance at the time of <paramref name="recordedOn"/>,
    /// and is participating (not exempt) in that subject instance.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// The grade value is not on the legal grading scale, the bucket does not exist, the student was not
    /// enrolled in the owning class instance at the time of <paramref name="recordedOn"/>, the student is
    /// exempt from the owning subject instance, <paramref name="recordedOn"/> is outside the school year, or a
    /// label or comment is too long. Label and comment are trimmed and stored as null when empty.
    /// </exception>
    /// <exception cref="ClosedYearException">The year is closed and not unlocked.</exception>
    public Task<Grade> RecordGradeAsync(
        int gradeBucketId,
        int studentId,
        decimal value,
        DateOnly recordedOn,
        string? label = null,
        string? comment = null,
        CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("GradeService.RecordGrade", async () =>
        {
            GradingScale.Validate(value);
            var normalizedLabel = NormalizeOptional(label, "A grade label", MaxLabelLength);
            var normalizedComment = NormalizeOptional(comment, "A grade comment", MaxCommentLength);

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var bucket = await context.GradeBuckets
                .Include(b => b.SubjectInstance).ThenInclude(si => si.ClassInstance).ThenInclude(ci => ci.SchoolYear)
                .FirstOrDefaultAsync(b => b.Id == gradeBucketId, cancellationToken)
                ?? throw new InvariantViolationException($"Grade bucket {gradeBucketId} does not exist.");

            await EnsureGradableAsync(context, bucket, studentId, recordedOn, cancellationToken);
            SchoolYearDateRule.EnsureDateInYear(recordedOn, bucket.SubjectInstance.ClassInstance.SchoolYear);

            var grade = new Grade
            {
                GradeBucketId = gradeBucketId,
                StudentId = studentId,
                Value = value,
                RecordedOn = recordedOn,
                Label = normalizedLabel,
                Comment = normalizedComment,
            };
            context.Grades.Add(grade);
            await context.SaveChangesAsync(cancellationToken);

            return grade;
        });

    /// <summary>
    /// Records <paramref name="value"/> for <paramref name="studentId"/> in <paramref name="assessmentId"/>. The
    /// grade counts toward the assessment's bucket and takes the assessment's date as its
    /// <see cref="Grade.RecordedOn"/>.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// The value is not on the grading scale, the comment is too long, the assessment does not exist or is
    /// voided, the student was not enrolled on the assessment's date, the student is exempt, or the student
    /// already has an active grade for the assessment.
    /// </exception>
    /// <exception cref="ClosedYearException">The year is closed and not unlocked.</exception>
    public Task<Grade> RecordAssessmentGradeAsync(
        int assessmentId,
        int studentId,
        decimal value,
        string? comment = null,
        CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("GradeService.RecordAssessmentGrade", async () =>
        {
            GradingScale.Validate(value);
            var normalizedComment = NormalizeOptional(comment, "A grade comment", MaxCommentLength);

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var assessment = await context.Assessments
                .Include(a => a.GradeBucket).ThenInclude(b => b.SubjectInstance)
                .FirstOrDefaultAsync(a => a.Id == assessmentId, cancellationToken)
                ?? throw new InvariantViolationException($"Assessment {assessmentId} does not exist.");
            EnsureAssessmentActive(assessment);

            await EnsureGradableAsync(context, assessment.GradeBucket, studentId, assessment.Date, cancellationToken);
            await EnsureNoActiveGradeAsync(context, assessmentId, studentId, excludedGradeId: null, cancellationToken);

            var grade = new Grade
            {
                GradeBucketId = assessment.GradeBucketId,
                AssessmentId = assessmentId,
                StudentId = studentId,
                Value = value,
                RecordedOn = assessment.Date,
                Comment = normalizedComment,
            };
            context.Grades.Add(grade);
            await context.SaveChangesAsync(cancellationToken);

            return grade;
        });

    /// <summary>
    /// Changes an active grade's value, date, label and comment. Label and comment are trimmed and stored as
    /// null when empty. Saving identical values changes nothing.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// The grade does not exist, is voided or belongs to a voided assessment, the value is not on the grading
    /// scale, the student was not enrolled or is exempt on <paramref name="recordedOn"/>, the date is outside
    /// the school year, or a label or
    /// comment is too long.
    /// </exception>
    /// <exception cref="ClosedYearException">The year is closed and not unlocked.</exception>
    public Task<Grade> UpdateGradeAsync(
        int gradeId,
        decimal value,
        DateOnly recordedOn,
        string? label = null,
        string? comment = null,
        CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("GradeService.UpdateGrade", async () =>
        {
            GradingScale.Validate(value);
            var normalizedLabel = NormalizeOptional(label, "A grade label", MaxLabelLength);
            var normalizedComment = NormalizeOptional(comment, "A grade comment", MaxCommentLength);

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var grade = await LoadGradeAsync(context, gradeId, cancellationToken);
            if (grade.VoidedOn is not null)
            {
                throw new InvariantViolationException($"Grade {gradeId} is voided and cannot be changed; restore it first.");
            }

            if (grade.Assessment is { } assessment)
            {
                EnsureAssessmentActive(assessment);
            }

            await EnsureGradableAsync(context, grade.GradeBucket, grade.StudentId, recordedOn, cancellationToken);
            SchoolYearDateRule.EnsureDateInYear(recordedOn, grade.GradeBucket.SubjectInstance.ClassInstance.SchoolYear);

            grade.Value = value;
            grade.RecordedOn = recordedOn;
            grade.Label = normalizedLabel;
            grade.Comment = normalizedComment;
            await context.SaveChangesAsync(cancellationToken);

            return grade;
        });

    /// <summary>Voids an active grade so it no longer counts toward any average. The grade is kept.</summary>
    /// <exception cref="InvariantViolationException">The grade does not exist or is already voided.</exception>
    /// <exception cref="ClosedYearException">The year is closed and not unlocked.</exception>
    public Task VoidGradeAsync(int gradeId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("GradeService.VoidGrade", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var grade = await context.Grades.FirstOrDefaultAsync(g => g.Id == gradeId, cancellationToken)
                ?? throw new InvariantViolationException($"Grade {gradeId} does not exist.");
            if (grade.VoidedOn is not null)
            {
                throw new InvariantViolationException($"Grade {gradeId} is already voided.");
            }

            grade.VoidedOn = clock.Now;
            await context.SaveChangesAsync(cancellationToken);
        });

    /// <summary>
    /// Restores a voided grade. The enrollment and exemption rules are checked again for its date, because
    /// they may have changed since the grade was voided.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// The grade does not exist or is not voided, its assessment is voided, the student already has another
    /// active grade for the assessment, or the student was not enrolled or is exempt on the grade's date.
    /// </exception>
    /// <exception cref="ClosedYearException">The year is closed and not unlocked.</exception>
    public Task RestoreGradeAsync(int gradeId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("GradeService.RestoreGrade", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var grade = await LoadGradeAsync(context, gradeId, cancellationToken);
            if (grade.VoidedOn is null)
            {
                throw new InvariantViolationException($"Grade {gradeId} is not voided.");
            }

            if (grade.Assessment is { } assessment)
            {
                EnsureAssessmentActive(assessment);
                await EnsureNoActiveGradeAsync(context, assessment.Id, grade.StudentId, grade.Id, cancellationToken);
            }

            await EnsureGradableAsync(context, grade.GradeBucket, grade.StudentId, grade.RecordedOn, cancellationToken);

            grade.VoidedOn = null;
            await context.SaveChangesAsync(cancellationToken);
        });

    /// <summary>
    /// Sets the final grade of <paramref name="studentId"/> in <paramref name="subjectInstanceId"/> to
    /// <paramref name="value"/>, overriding the proposal derived from the subject average. The proposal itself is
    /// not stored and stays available next to the override. Setting the current override again changes nothing.
    /// </summary>
    /// <param name="subjectInstanceId">The subject instance the final grade is given in.</param>
    /// <param name="studentId">The student.</param>
    /// <param name="value">The final grade, a whole grade from 1 to 6.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The stored final grade row.</returns>
    /// <exception cref="InvariantViolationException">
    /// The value is not a legal final grade, the subject instance does not exist, the student has no enrollment in
    /// its class instance, or the student is exempt from the subject instance.
    /// </exception>
    /// <exception cref="ClosedYearException">The year is closed and not unlocked.</exception>
    /// <remarks>
    /// Enrollment is checked without a date: a final grade covers the whole year, so a student who has left the
    /// class can still get one for the time they were in it.
    /// </remarks>
    public Task<FinalGrade> SetFinalGradeOverrideAsync(
        int subjectInstanceId, int studentId, decimal value, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("GradeService.SetFinalGradeOverride", async () =>
        {
            GradingScale.ValidateFinalGrade(value);

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var classInstanceId = await context.SubjectInstances
                .Where(si => si.Id == subjectInstanceId)
                .Select(si => (int?)si.ClassInstanceId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvariantViolationException($"Subject instance {subjectInstanceId} does not exist.");

            var isEnrolled = await context.Enrollments.AnyAsync(
                e => e.StudentId == studentId && e.ClassInstanceId == classInstanceId, cancellationToken);
            if (!isEnrolled)
            {
                throw new InvariantViolationException(
                    $"Student {studentId} is not enrolled in class instance {classInstanceId} (which owns subject instance " +
                    $"{subjectInstanceId}) and cannot receive a final grade there.");
            }

            await EnsureNotExemptAsync(context, subjectInstanceId, studentId, "a final grade", cancellationToken);

            var finalGrade = await context.FinalGrades.FirstOrDefaultAsync(
                f => f.SubjectInstanceId == subjectInstanceId && f.StudentId == studentId, cancellationToken);
            if (finalGrade is null)
            {
                finalGrade = new FinalGrade { SubjectInstanceId = subjectInstanceId, StudentId = studentId };
                context.FinalGrades.Add(finalGrade);
            }

            // An unchanged value leaves the entity unmodified, so no write and no audit entry happen.
            finalGrade.OverrideValue = value;
            await context.SaveChangesAsync(cancellationToken);

            return finalGrade;
        });

    /// <summary>
    /// Clears the final grade override of <paramref name="studentId"/> in <paramref name="subjectInstanceId"/>, so
    /// the proposal applies again. The row is kept with no override, because nothing is ever deleted.
    /// </summary>
    /// <param name="subjectInstanceId">The subject instance the final grade is given in.</param>
    /// <param name="studentId">The student.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <exception cref="InvariantViolationException">The student has no final grade override in the subject instance.</exception>
    /// <exception cref="ClosedYearException">The year is closed and not unlocked.</exception>
    public Task ClearFinalGradeOverrideAsync(int subjectInstanceId, int studentId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("GradeService.ClearFinalGradeOverride", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var finalGrade = await context.FinalGrades.FirstOrDefaultAsync(
                f => f.SubjectInstanceId == subjectInstanceId && f.StudentId == studentId, cancellationToken);
            if (finalGrade?.OverrideValue is null)
            {
                throw new InvariantViolationException(
                    $"Student {studentId} has no final grade override in subject instance {subjectInstanceId}.");
            }

            finalGrade.OverrideValue = null;
            await context.SaveChangesAsync(cancellationToken);
        });

    /// <summary>
    /// Checks that the student was enrolled in the bucket's class instance on <paramref name="date"/> and is not
    /// exempt from the bucket's subject instance. <paramref name="bucket"/> must have its subject instance loaded.
    /// </summary>
    private static async Task EnsureGradableAsync(
        AppDbContext context, GradeBucket bucket, int studentId, DateOnly date, CancellationToken cancellationToken)
    {
        var classInstanceId = bucket.SubjectInstance.ClassInstanceId;
        var subjectInstanceId = bucket.SubjectInstanceId;

        // Scoped "at the time of the date", not "ever": a student who has since left (or has not yet joined as
        // of that date) does not satisfy it, even if some enrollment row for that class instance exists.
        var enrollment = await context.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.ClassInstanceId == classInstanceId, cancellationToken);
        if (enrollment is null || !EnrollmentActivity.IsActiveOn(enrollment.JoinedOn, enrollment.LeftOn, date))
        {
            throw new InvariantViolationException(
                $"Student {studentId} was not enrolled in class instance {classInstanceId} (which owns grade bucket {bucket.Id}) " +
                $"on {date:O}.");
        }

        await EnsureNotExemptAsync(context, subjectInstanceId, studentId, "a grade", cancellationToken);
    }

    /// <summary>
    /// Checks that the student is not exempt from the subject instance. The caller must already have confirmed
    /// the enrollment. <paramref name="what"/> names what the student would receive, for the message.
    /// </summary>
    private static async Task EnsureNotExemptAsync(
        AppDbContext context, int subjectInstanceId, int studentId, string what, CancellationToken cancellationToken)
    {
        var participationMode = await context.SubjectParticipations
            .Where(p => p.SubjectInstanceId == subjectInstanceId && p.StudentId == studentId)
            .Select(p => (ParticipationMode?)p.Mode)
            .FirstOrDefaultAsync(cancellationToken);

        // Default is participation by class enrollment; a SubjectParticipation row exists only where the
        // student deviates. Enrollment was already confirmed, so "no override row" means participating.
        if (participationMode == ParticipationMode.Exempt)
        {
            throw new InvariantViolationException(
                $"Student {studentId} is exempt from subject instance {subjectInstanceId} and cannot receive {what} there.");
        }
    }

    private static async Task<Grade> LoadGradeAsync(AppDbContext context, int gradeId, CancellationToken cancellationToken) =>
        await context.Grades
            .Include(g => g.GradeBucket).ThenInclude(b => b.SubjectInstance).ThenInclude(si => si.ClassInstance).ThenInclude(ci => ci.SchoolYear)
            .Include(g => g.Assessment)
            .FirstOrDefaultAsync(g => g.Id == gradeId, cancellationToken)
        ?? throw new InvariantViolationException($"Grade {gradeId} does not exist.");

    private static void EnsureAssessmentActive(Assessment assessment)
    {
        if (assessment.VoidedOn is not null)
        {
            throw new InvariantViolationException($"Assessment {assessment.Id} is voided; restore it first.");
        }
    }

    private static async Task EnsureNoActiveGradeAsync(
        AppDbContext context, int assessmentId, int studentId, int? excludedGradeId, CancellationToken cancellationToken)
    {
        var exists = await context.Grades.AnyAsync(
            g => g.AssessmentId == assessmentId && g.StudentId == studentId && g.VoidedOn == null && g.Id != excludedGradeId,
            cancellationToken);
        if (exists)
        {
            throw new InvariantViolationException(
                $"Student {studentId} already has an active grade for assessment {assessmentId}.");
        }
    }

    private static string? NormalizeOptional(string? value, string description, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new InvariantViolationException(
                $"{description} must not exceed {maxLength} characters (was {trimmed.Length}).");
        }

        return trimmed;
    }
}
