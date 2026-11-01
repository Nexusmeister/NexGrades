using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;

namespace NexGrades.Domain.Services;

/// <summary>
/// Enforces the enrollment invariant that the database schema alone cannot express (see the remarks on
/// <see cref="Data.Entities.Enrollment"/>): a student may have at most one enrollment per school year, a
/// rule that spans every <see cref="ClassInstance"/> belonging to that year, not just the one being enrolled
/// into.
/// </summary>
/// <remarks>
/// Like every Domain service, this takes an <see cref="IDbContextFactory{TContext}"/> rather than an
/// <see cref="AppDbContext"/> and opens one short-lived context per public method. The services are resolved
/// from the root provider by the WPF navigation (no DI scope exists around a page), so a scoped context would
/// either fail scope validation or silently become one long-lived, never-disposed context shared by the whole
/// app. A context per operation also gives each use case its own unit of work and transaction.
/// </remarks>
public sealed class EnrollmentService(IDbContextFactory<AppDbContext> contextFactory)
{
    /// <summary>
    /// Creates a new <see cref="Enrollment"/> for <paramref name="studentId"/> in <paramref name="classInstanceId"/>,
    /// inside a transaction that first checks for an existing enrollment for that student in any class
    /// instance of the same school year.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// The student already has an enrollment somewhere in that school year, or the class instance does not exist.
    /// </exception>
    public Task<Enrollment> CreateEnrollmentAsync(
        int studentId, int classInstanceId, DateTime joinedOn, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("EnrollmentService.CreateEnrollment", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var classInstance = await context.ClassInstances
                .FirstOrDefaultAsync(ci => ci.Id == classInstanceId, cancellationToken)
                ?? throw new InvariantViolationException($"Class instance {classInstanceId} does not exist.");

            await EnrollmentInvariants.EnsureNoConflictingEnrollmentAsync(
                context, studentId, classInstance.SchoolYearId, cancellationToken);

            var enrollment = new Enrollment
            {
                StudentId = studentId,
                ClassInstanceId = classInstanceId,
                JoinedOn = joinedOn,
            };
            context.Enrollments.Add(enrollment);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return enrollment;
        });
}
