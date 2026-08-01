using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Grading;

namespace NexGrades.Domain.Services;

/// <summary>
/// Records grades after validating that the student must be enrolled in the class instance that owns the
/// grade's bucket, and the student must be participating (not exempt) in that subject instance.
/// </summary>
public sealed class GradeService(AppDbContext context)
{
    /// <summary>
    /// Records a grade after validating that <paramref name="studentId"/> was enrolled in the class instance
    /// owning <paramref name="gradeBucketId"/>'s subject instance at the time of <paramref name="recordedOn"/>,
    /// and is participating (not exempt) in that subject instance.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// The grade value is not on the legal grading scale, the bucket does not exist, the student was not
    /// enrolled in the owning class instance at the time of <paramref name="recordedOn"/>, or the student is
    /// exempt from the owning subject instance.
    /// </exception>
    public async Task<Grade> RecordGradeAsync(
        int gradeBucketId,
        int studentId,
        decimal value,
        DateOnly recordedOn,
        string? label = null,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        GradingScale.Validate(value);

        var bucket = await context.GradeBuckets
            .Include(b => b.SubjectInstance)
            .FirstOrDefaultAsync(b => b.Id == gradeBucketId, cancellationToken)
            ?? throw new InvariantViolationException($"Grade bucket {gradeBucketId} does not exist.");

        var classInstanceId = bucket.SubjectInstance.ClassInstanceId;
        var subjectInstanceId = bucket.SubjectInstanceId;

        // Scoped "at the time of RecordedOn", not "ever": a student who has since left (or has not yet
        // joined as of that date) does not satisfy it, even if some enrollment row for that class instance
        // exists.
        var enrollment = await context.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.ClassInstanceId == classInstanceId, cancellationToken);
        var recordedOnAsDateTime = recordedOn.ToDateTime(TimeOnly.MinValue);
        if (enrollment is null || !EnrollmentActivity.IsActive(enrollment, recordedOnAsDateTime))
        {
            throw new InvariantViolationException(
                $"Student {studentId} was not enrolled in class instance {classInstanceId} (which owns grade bucket {gradeBucketId}) " +
                $"on {recordedOn:O}.");
        }

        var participationMode = await context.SubjectParticipations
            .Where(p => p.SubjectInstanceId == subjectInstanceId && p.StudentId == studentId)
            .Select(p => (ParticipationMode?)p.Mode)
            .FirstOrDefaultAsync(cancellationToken);

        // Default is participation by class enrollment; a SubjectParticipation row exists only where the
        // student deviates. Enrollment was already confirmed above, so "no override row" means participating.
        var isParticipating = participationMode != ParticipationMode.Exempt;
        if (!isParticipating)
        {
            throw new InvariantViolationException(
                $"Student {studentId} is exempt from subject instance {subjectInstanceId} and cannot receive a grade there.");
        }

        var grade = new Grade
        {
            GradeBucketId = gradeBucketId,
            StudentId = studentId,
            Value = value,
            RecordedOn = recordedOn,
            Label = label,
            Comment = comment,
        };
        context.Grades.Add(grade);
        await context.SaveChangesAsync(cancellationToken);

        return grade;
    }
}
