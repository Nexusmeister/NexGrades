using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Time;

namespace NexGrades.Domain.Services;

/// <summary>
/// Implements the two year-close dispositions: rollover and archive. Both operations are pure status changes
/// plus new rows: no historic row is ever mutated or deleted.
/// </summary>
public sealed class RolloverService(AppDbContext context, IClock clock)
{
    /// <summary>
    /// Rolls a class instance forward into a new school year: the old instance is marked
    /// <see cref="ClassInstanceStatus.RolledForward"/>, and a new, independent <see cref="ClassInstance"/> is
    /// created carrying forward active enrollments, subject instances (with buckets copied fresh from the
    /// <em>current</em> <see cref="BucketTemplate"/> rows, not the old snapshot), and subject participation
    /// overrides for the students who carried over.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// The class instance does not exist, or a carried-forward student would end up double-enrolled in the
    /// target school year.
    /// </exception>
    public async Task<ClassInstance> RollClassInstanceForwardAsync(
        int classInstanceId, int targetSchoolYearId, string newName, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var oldInstance = await context.ClassInstances
            .Include(ci => ci.Enrollments)
            .Include(ci => ci.SubjectInstances).ThenInclude(si => si.GradeBuckets)
            .Include(ci => ci.SubjectInstances).ThenInclude(si => si.SubjectParticipations)
            .Include(ci => ci.SubjectInstances).ThenInclude(si => si.Subject).ThenInclude(s => s.BucketTemplates)
            .FirstOrDefaultAsync(ci => ci.Id == classInstanceId, cancellationToken)
            ?? throw new InvariantViolationException($"Class instance {classInstanceId} does not exist.");

        if (oldInstance.Status != ClassInstanceStatus.Active)
        {
            throw new InvariantViolationException(
                $"Class instance {classInstanceId} is not Active (current status: {oldInstance.Status}); it has already been rolled forward or archived.");
        }

        // Step 1: old instance -> RolledForward. Nothing else about it changes.
        oldInstance.Status = ClassInstanceStatus.RolledForward;

        // Step 2: new instance, same cohort and teacher, new name, in the target year.
        var newInstance = new ClassInstance
        {
            ClassGroupId = oldInstance.ClassGroupId,
            SchoolYearId = targetSchoolYearId,
            Name = newName,
            TeacherId = oldInstance.TeacherId,
            Status = ClassInstanceStatus.Active,
        };
        context.ClassInstances.Add(newInstance);

        // Saved now (rather than at the end) so newInstance.Id is a real, persisted key before it is used
        // as a foreign key below — required for the closed-year audit interceptor's entity resolution, and
        // simply good practice for multi-step graph construction.
        await context.SaveChangesAsync(cancellationToken);

        // Step 3: carry forward active enrollments, going through the same invariant check used by
        // EnrollmentService, so a student can never end up double-enrolled in the target year.
        var activeEnrollments = oldInstance.Enrollments
            .Where(e => EnrollmentActivity.IsActive(e, clock))
            .ToList();

        foreach (var enrollment in activeEnrollments)
        {
            await EnrollmentInvariants.EnsureNoConflictingEnrollmentAsync(
                context, enrollment.StudentId, targetSchoolYearId, cancellationToken);

            context.Enrollments.Add(new Enrollment
            {
                StudentId = enrollment.StudentId,
                ClassInstanceId = newInstance.Id,
                JoinedOn = clock.Now,
            });
        }

        var carriedStudentIds = activeEnrollments.Select(e => e.StudentId).ToHashSet();

        // Step 4 & 5: one new SubjectInstance per old one, buckets copied from the CURRENT live
        // BucketTemplate rows (not the old instance's GradeBucket snapshot — this is the mechanism that
        // lets a template edit affect only future years), and participation overrides copied (not shared)
        // for the students who carried over.
        foreach (var oldSubjectInstance in oldInstance.SubjectInstances)
        {
            var newSubjectInstance = new SubjectInstance
            {
                ClassInstanceId = newInstance.Id,
                SubjectId = oldSubjectInstance.SubjectId,
                TeacherId = oldSubjectInstance.TeacherId,
            };
            context.SubjectInstances.Add(newSubjectInstance);
            await context.SaveChangesAsync(cancellationToken);

            foreach (var template in oldSubjectInstance.Subject.BucketTemplates)
            {
                context.GradeBuckets.Add(new GradeBucket
                {
                    SubjectInstanceId = newSubjectInstance.Id,
                    Name = template.Name,
                    Weight = template.Weight,
                    SortOrder = template.SortOrder,
                });
            }

            foreach (var participation in oldSubjectInstance.SubjectParticipations.Where(p => carriedStudentIds.Contains(p.StudentId)))
            {
                context.SubjectParticipations.Add(new SubjectParticipation
                {
                    SubjectInstanceId = newSubjectInstance.Id,
                    StudentId = participation.StudentId,
                    Mode = participation.Mode,
                    From = participation.From,
                    To = participation.To,
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return newInstance;
    }

    /// <summary>
    /// Archives a class instance: marks it <see cref="ClassInstanceStatus.Archived"/> and unassigns its
    /// teacher, without creating any new row for the next year and without touching enrollments, students,
    /// grades, or notes. Whether the owning <see cref="ClassGroup"/> should also be marked
    /// <see cref="ClassGroupStatus.Ended"/> is a teacher decision the caller supplies explicitly
    /// (<paramref name="endClassGroup"/>) — it cannot be inferred from the model.
    /// </summary>
    public async Task ArchiveClassInstanceAsync(int classInstanceId, bool endClassGroup, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var instance = await context.ClassInstances
            .Include(ci => ci.ClassGroup)
            .FirstOrDefaultAsync(ci => ci.Id == classInstanceId, cancellationToken)
            ?? throw new InvariantViolationException($"Class instance {classInstanceId} does not exist.");

        if (instance.Status != ClassInstanceStatus.Active)
        {
            throw new InvariantViolationException(
                $"Class instance {classInstanceId} is not Active (current status: {instance.Status}); it has already been rolled forward or archived.");
        }

        instance.Status = ClassInstanceStatus.Archived;
        instance.TeacherId = null;

        if (endClassGroup)
        {
            instance.ClassGroup.Status = ClassGroupStatus.Ended;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
