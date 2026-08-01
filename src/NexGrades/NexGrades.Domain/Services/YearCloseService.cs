using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Time;

namespace NexGrades.Domain.Services;

/// <summary>
/// Implements the year close workflow: reports readiness (blocking issues and advisory warnings), then, once
/// nothing blocks it, flips the year to <see cref="SchoolYearStatus.Closed"/>.
/// </summary>
public sealed class YearCloseService(AppDbContext context, IClock clock)
{
    /// <summary>
    /// Reports what would prevent <paramref name="schoolYearId"/> from closing right now (blocking), and
    /// what looks unfinished but would not prevent the close (advisory).
    /// </summary>
    public async Task<CloseReadiness> GetCloseReadinessAsync(int schoolYearId, CancellationToken cancellationToken = default)
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

        var blockingIssues = new List<string>();
        var warnings = new List<string>();

        foreach (var classInstance in classInstances)
        {
            if (classInstance.Status == ClassInstanceStatus.Active)
            {
                blockingIssues.Add(
                    $"Class instance '{classInstance.Name}' (Id={classInstance.Id}) is still Active; " +
                    "it must be rolled forward or archived before the year can close.");
            }

            var activeStudentIds = classInstance.Enrollments
                .Where(e => EnrollmentActivity.IsActive(e, clock))
                .Select(e => e.StudentId)
                .ToHashSet();

            foreach (var subjectInstance in classInstance.SubjectInstances)
            {
                var totalGradeCount = subjectInstance.GradeBuckets.Sum(gb => gb.Grades.Count);
                if (totalGradeCount == 0)
                {
                    warnings.Add(
                        $"Subject '{subjectInstance.Subject.Name}' in class '{classInstance.Name}' has no grades recorded in any bucket.");
                }

                var weightSum = subjectInstance.GradeBuckets.Sum(gb => gb.Weight);
                if (weightSum != 100m)
                {
                    warnings.Add(
                        $"Subject '{subjectInstance.Subject.Name}' in class '{classInstance.Name}' has bucket weights totalling {weightSum}, not 100.");
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
                        warnings.Add(
                            $"Student {studentId} participates in subject '{subjectInstance.Subject.Name}' in class '{classInstance.Name}' but has no grades recorded.");
                    }
                }
            }
        }

        return new CloseReadiness(blockingIssues, warnings);
    }

    /// <summary>
    /// Closes <paramref name="schoolYearId"/>: sets <see cref="SchoolYear.Status"/> to
    /// <see cref="SchoolYearStatus.Closed"/> and <see cref="SchoolYear.ClosedOn"/> to now, but only once
    /// every blocking issue from <see cref="GetCloseReadinessAsync"/> has been resolved.
    /// </summary>
    /// <exception cref="InvariantViolationException">Blocking issues remain; they are listed in the message.</exception>
    public async Task CloseSchoolYearAsync(int schoolYearId, CancellationToken cancellationToken = default)
    {
        var readiness = await GetCloseReadinessAsync(schoolYearId, cancellationToken);
        if (!readiness.CanClose)
        {
            throw new InvariantViolationException(
                $"Cannot close school year {schoolYearId}: " + string.Join(" ", readiness.BlockingIssues));
        }

        var schoolYear = await context.SchoolYears.FirstAsync(sy => sy.Id == schoolYearId, cancellationToken);
        schoolYear.Status = SchoolYearStatus.Closed;
        schoolYear.ClosedOn = clock.Now;

        await context.SaveChangesAsync(cancellationToken);
    }
}
