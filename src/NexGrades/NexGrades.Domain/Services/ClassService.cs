using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Auditing;
using NexGrades.Domain.Exceptions;

namespace NexGrades.Domain.Services;

/// <summary>
/// Creates and renames classes. A brand-new class starts its own cohort in the Active school year.
/// </summary>
/// <param name="contextFactory">Creates the short-lived context that each call opens.</param>
/// <remarks>
/// Writes to closed years are rejected and audited by <see cref="ClosedYearAuditInterceptor"/>; this service
/// only adds the class-specific rules.
/// </remarks>
public sealed class ClassService(IDbContextFactory<AppDbContext> contextFactory)
{
    /// <summary>
    /// Creates a <see cref="ClassGroup"/> and its first <see cref="ClassInstance"/> in the Active school year,
    /// both named <paramref name="name"/>, in one transaction. A new class is its own cohort, as opposed to one
    /// rolled forward from a prior year.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// The name is empty, or there is not exactly one Active school year.
    /// </exception>
    public Task<ClassInstance> CreateClassAsync(string name, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("ClassService.CreateClass", async () =>
        {
            var normalizedName = RequiredText.Normalize(name, "A class name");

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var activeYearIds = await context.SchoolYears
                .AsNoTracking()
                .Where(sy => sy.Status == SchoolYearStatus.Active)
                .Select(sy => sy.Id)
                .Take(2)
                .ToListAsync(cancellationToken);

            if (activeYearIds.Count == 0)
            {
                throw new InvariantViolationException("There is no Active school year to create a class in.");
            }

            if (activeYearIds.Count > 1)
            {
                throw new InvariantViolationException(
                    $"School years {activeYearIds[0]} and {activeYearIds[1]} are both Active; " +
                    "a class can only be created when exactly one school year is Active.");
            }

            var yearId = activeYearIds[0];
            var classGroup = new ClassGroup
            {
                CohortLabel = normalizedName,
                FoundedInSchoolYearId = yearId,
            };
            var classInstance = new ClassInstance
            {
                ClassGroup = classGroup,
                SchoolYearId = yearId,
                Name = normalizedName,
            };
            context.ClassGroups.Add(classGroup);
            context.ClassInstances.Add(classInstance);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return classInstance;
        });

    /// <summary>
    /// Renames an Active class instance. The cohort label of its <see cref="ClassGroup"/> is left alone. Saving
    /// the same name is a no-op.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// The name is empty, the class does not exist, or it is no longer Active (rolled forward or archived).
    /// </exception>
    /// <exception cref="ClosedYearException">The class's school year is closed and not unlocked.</exception>
    public Task RenameClassAsync(int classInstanceId, string newName, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("ClassService.RenameClass", async () =>
        {
            var normalizedName = RequiredText.Normalize(newName, "A class name");

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var classInstance = await context.ClassInstances
                .FirstOrDefaultAsync(ci => ci.Id == classInstanceId, cancellationToken)
                ?? throw new InvariantViolationException($"Class instance {classInstanceId} does not exist.");

            if (classInstance.Status != ClassInstanceStatus.Active)
            {
                throw new InvariantViolationException(
                    $"Class instance {classInstanceId} is {classInstance.Status}; " +
                    "a rolled-forward or archived class keeps its historic name.");
            }

            if (classInstance.Name == normalizedName)
            {
                return;
            }

            classInstance.Name = normalizedName;
            await context.SaveChangesAsync(cancellationToken);
        });
}
