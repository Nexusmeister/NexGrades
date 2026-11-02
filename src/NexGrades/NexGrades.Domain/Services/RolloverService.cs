using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Time;

namespace NexGrades.Domain.Services;

/// <summary>
/// Implements the two year-close dispositions: rollover and archive. Both operations are pure status changes
/// plus new rows: no historic row is ever mutated or deleted. Also reports where each class stands, for the
/// guided end-of-year flow.
/// </summary>
/// <param name="contextFactory">Creates the short-lived context that each public method opens.</param>
/// <param name="clock">Source of "now" for deciding which enrollments are active and for their new join date.</param>
/// <remarks>Opens a short-lived context per call; see <see cref="EnrollmentService"/> for why.</remarks>
public sealed class RolloverService(IDbContextFactory<AppDbContext> contextFactory, IClock clock)
{
    /// <summary>
    /// Rolls a class instance forward into a new school year: the old instance is marked
    /// <see cref="ClassInstanceStatus.RolledForward"/>, and a new, independent <see cref="ClassInstance"/> is
    /// created carrying forward active enrollments, subject instances (with buckets copied fresh from the
    /// <em>current</em> <see cref="BucketTemplate"/> rows, not the old snapshot), and subject participation
    /// overrides for the students who carried over.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// The new name is empty, the class instance or the target year does not exist, the class is not Active, the
    /// target year is not Planned or does not start after the class's year, the cohort already has a class in the
    /// target year, or a carried-forward student would end up double-enrolled in the target school year.
    /// </exception>
    public Task<ClassInstance> RollClassInstanceForwardAsync(
        int classInstanceId, int targetSchoolYearId, string newName, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("RolloverService.RollClassInstanceForward", async () =>
        {
            var name = RequiredText.Normalize(newName, "A class name");

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
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

            await EnsureTargetYearAsync(context, oldInstance, targetSchoolYearId, cancellationToken);

            // Step 1: old instance -> RolledForward. Nothing else about it changes.
            oldInstance.Status = ClassInstanceStatus.RolledForward;

            // Step 2: new instance, same cohort and teacher, new name, in the target year.
            var newInstance = new ClassInstance
            {
                ClassGroupId = oldInstance.ClassGroupId,
                SchoolYearId = targetSchoolYearId,
                Name = name,
                TeacherId = oldInstance.TeacherId,
                Status = ClassInstanceStatus.Active,
            };
            context.ClassInstances.Add(newInstance);

            // Saved now (rather than at the end) so newInstance.Id is a real, persisted key before it is used
            // as a foreign key below — required for the closed-year audit interceptor's entity resolution, and
            // simply good practice for multi-step graph construction.
            await context.SaveChangesAsync(cancellationToken);

            var carriedStudentIds = await CarryForwardEnrollmentsAsync(
                context, oldInstance, newInstance.Id, targetSchoolYearId, cancellationToken);

            await CopySubjectInstancesAsync(context, oldInstance, newInstance.Id, carriedStudentIds, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return newInstance;
        });

    /// <summary>
    /// Checks that <paramref name="oldInstance"/> may roll into <paramref name="targetSchoolYearId"/>: the year
    /// exists, is Planned and starts after the class's own year, and the cohort has no class there yet.
    /// </summary>
    private static async Task EnsureTargetYearAsync(
        AppDbContext context, ClassInstance oldInstance, int targetSchoolYearId, CancellationToken cancellationToken)
    {
        var sourceYear = await context.SchoolYears.AsNoTracking()
            .FirstAsync(sy => sy.Id == oldInstance.SchoolYearId, cancellationToken);
        var targetYear = await context.SchoolYears.AsNoTracking()
            .FirstOrDefaultAsync(sy => sy.Id == targetSchoolYearId, cancellationToken)
            ?? throw new InvariantViolationException($"School year {targetSchoolYearId} does not exist.");

        if (targetYear.Status != SchoolYearStatus.Planned)
        {
            throw new InvariantViolationException(
                $"School year {targetSchoolYearId} is not Planned (current status: {targetYear.Status}); a class can only be rolled forward into a Planned year.");
        }

        if (targetYear.Id == sourceYear.Id || targetYear.StartDate <= sourceYear.StartDate)
        {
            throw new InvariantViolationException(
                $"School year {targetSchoolYearId} does not start after school year {sourceYear.Id}.");
        }

        var groupAlreadyInTargetYear = await context.ClassInstances.AsNoTracking().AnyAsync(
            ci => ci.ClassGroupId == oldInstance.ClassGroupId && ci.SchoolYearId == targetSchoolYearId, cancellationToken);
        if (groupAlreadyInTargetYear)
        {
            throw new InvariantViolationException(
                $"The class group of class instance {oldInstance.Id} already has a class in school year {targetSchoolYearId}.");
        }
    }

    /// <summary>
    /// Reports, for every class of <paramref name="schoolYearId"/>, whether it still needs a decision, how many
    /// students would carry over (or did), a suggested name for the next year, and its successor in
    /// <paramref name="targetSchoolYearId"/> when that year is given. The guided flow derives its resume point
    /// from this.
    /// </summary>
    /// <exception cref="InvariantViolationException">A given school year does not exist.</exception>
    public Task<IReadOnlyList<ClassRolloverState>> GetClassRolloverStatesAsync(
        int schoolYearId, int? targetSchoolYearId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync<IReadOnlyList<ClassRolloverState>>("RolloverService.GetClassRolloverStates", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            if (!await context.SchoolYears.AsNoTracking().AnyAsync(sy => sy.Id == schoolYearId, cancellationToken))
            {
                throw new InvariantViolationException($"School year {schoolYearId} does not exist.");
            }

            var classInstances = await context.ClassInstances.AsNoTracking()
                .Where(ci => ci.SchoolYearId == schoolYearId)
                .Include(ci => ci.ClassGroup)
                .Include(ci => ci.Enrollments)
                .ToListAsync(cancellationToken);

            var successors = new Dictionary<int, ClassInstance>();
            if (targetSchoolYearId is { } targetId)
            {
                if (!await context.SchoolYears.AsNoTracking().AnyAsync(sy => sy.Id == targetId, cancellationToken))
                {
                    throw new InvariantViolationException($"School year {targetId} does not exist.");
                }

                var successorInstances = await context.ClassInstances.AsNoTracking()
                    .Where(ci => ci.SchoolYearId == targetId)
                    .Include(ci => ci.Enrollments)
                    .ToListAsync(cancellationToken);
                successors = successorInstances.ToDictionary(ci => ci.ClassGroupId);
            }

            return [.. classInstances
                .OrderBy(ci => ci.Name, StringComparer.OrdinalIgnoreCase)
                .Select(ci =>
                {
                    successors.TryGetValue(ci.ClassGroupId, out var successor);
                    var carryOver = ci.Status switch
                    {
                        ClassInstanceStatus.Active => ci.Enrollments.Count(e => EnrollmentActivity.IsActive(e, clock)),
                        ClassInstanceStatus.RolledForward => successor?.Enrollments.Count ?? 0,
                        _ => 0,
                    };

                    return new ClassRolloverState(
                        ci.Id,
                        ci.Name,
                        ci.Status,
                        carryOver,
                        ClassNameSuggestion.SuggestNextName(ci.Name),
                        successor?.Id,
                        successor?.Name,
                        ci.ClassGroup.Status == ClassGroupStatus.Ended);
                })];
        });

    /// <summary>
    /// Step 3 of the rollover: carries forward active enrollments into the new instance, going through the
    /// same invariant check used by <see cref="EnrollmentService"/>. Returns the ids of the carried students.
    /// </summary>
    private async Task<HashSet<int>> CarryForwardEnrollmentsAsync(
        AppDbContext context,
        ClassInstance oldInstance,
        int newInstanceId,
        int targetSchoolYearId,
        CancellationToken cancellationToken)
    {
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
                ClassInstanceId = newInstanceId,
                JoinedOn = clock.Now,
            });
        }

        return [.. activeEnrollments.Select(e => e.StudentId)];
    }

    /// <summary>
    /// Steps 4 and 5 of the rollover: one new <see cref="SubjectInstance"/> per old one, with buckets copied
    /// from the current templates and participation overrides copied for the students who carried over.
    /// </summary>
    private static async Task CopySubjectInstancesAsync(
        AppDbContext context,
        ClassInstance oldInstance,
        int newInstanceId,
        HashSet<int> carriedStudentIds,
        CancellationToken cancellationToken)
    {
        // Step 4 & 5: one new SubjectInstance per old one, buckets copied from the CURRENT live
        // BucketTemplate rows (not the old instance's GradeBucket snapshot — this is the mechanism that
        // lets a template edit affect only future years), and participation overrides copied (not shared)
        // for the students who carried over.
        foreach (var oldSubjectInstance in oldInstance.SubjectInstances)
        {
            var newSubjectInstance = new SubjectInstance
            {
                ClassInstanceId = newInstanceId,
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
    }

    /// <summary>
    /// Archives a class instance: marks it <see cref="ClassInstanceStatus.Archived"/> and unassigns its
    /// teacher, without creating any new row for the next year and without touching enrollments, students,
    /// grades, or notes. Whether the owning <see cref="ClassGroup"/> should also be marked
    /// <see cref="ClassGroupStatus.Ended"/> is a teacher decision the caller supplies explicitly
    /// (<paramref name="endClassGroup"/>) — it cannot be inferred from the model.
    /// </summary>
    public Task ArchiveClassInstanceAsync(int classInstanceId, bool endClassGroup, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("RolloverService.ArchiveClassInstance", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
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
        });
}
