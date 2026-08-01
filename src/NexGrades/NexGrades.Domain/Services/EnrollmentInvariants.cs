using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Domain.Exceptions;

namespace NexGrades.Domain.Services;

/// <summary>
/// The "at most one enrollment per student per school year" check, factored out so both
/// <see cref="EnrollmentService"/> and <see cref="RolloverService"/> can reuse the exact same rule without
/// either of them opening a nested transaction on top of the caller's. Callers own the transaction; this
/// class only performs the read and throws.
/// </summary>
internal static class EnrollmentInvariants
{
    public static async Task EnsureNoConflictingEnrollmentAsync(
        AppDbContext context, int studentId, int schoolYearId, CancellationToken cancellationToken)
    {
        var conflicting = await context.Enrollments
            .AnyAsync(e => e.StudentId == studentId && e.ClassInstance.SchoolYearId == schoolYearId, cancellationToken);

        if (conflicting)
        {
            throw new InvariantViolationException(
                $"Student {studentId} already has an enrollment in a class instance belonging to school year {schoolYearId}. " +
                "A student may have at most one enrollment per school year.");
        }
    }
}
