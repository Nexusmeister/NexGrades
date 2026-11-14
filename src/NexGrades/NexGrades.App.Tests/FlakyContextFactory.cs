using Microsoft.EntityFrameworkCore;
using NexGrades.Data;

namespace NexGrades.App.Tests;

/// <summary>
/// Hands out contexts from <paramref name="inner"/> for the first <paramref name="allowedContexts"/> calls and then
/// throws, so a test can let a write succeed and make the read that follows it fail. <see cref="Allow"/> grants more.
/// </summary>
internal sealed class FlakyContextFactory(IDbContextFactory<AppDbContext> inner, int allowedContexts) : IDbContextFactory<AppDbContext>
{
    private int _remaining = allowedContexts;

    /// <summary>Lets the next <paramref name="count"/> context requests succeed, replacing what was left.</summary>
    /// <param name="count">How many requests may succeed from now on.</param>
    public void Allow(int count) => Volatile.Write(ref _remaining, count);

    public AppDbContext CreateDbContext() => Take().CreateDbContext();

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Take().CreateDbContextAsync(cancellationToken);

    private IDbContextFactory<AppDbContext> Take()
    {
        if (Interlocked.Decrement(ref _remaining) < 0)
        {
            throw new InvalidOperationException("The database is not available.");
        }

        return inner;
    }
}
