using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NexGrades.Tests;

/// <summary>
/// Fails a save after the rows were written but before the caller can commit its explicit transaction, so a
/// service that forgot the transaction leaves rows behind and its test fails.
/// </summary>
/// <typeparam name="TEntity">The entity type whose presence in the change tracker triggers the failure.</typeparam>
internal sealed class ThrowAfterSaveInterceptor<TEntity> : SaveChangesInterceptor
    where TEntity : class
{
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        ThrowIfTracked(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ThrowIfTracked(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private static void ThrowIfTracked(DbContext? context)
    {
        if (context is not null && context.ChangeTracker.Entries<TEntity>().Any())
        {
            throw new InvalidOperationException($"Simulated failure after saving {typeof(TEntity).Name} rows.");
        }
    }
}
