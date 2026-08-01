using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NexGrades.Data;
using NexGrades.Domain.Time;

namespace NexGrades.Tests;

/// <summary>
/// A real SQLite database file (migrated, on disk, not in-memory), so tests exercise the actual provider
/// (decimal-as-TEXT mapping, string-enum conversions, unique indexes, transactions) rather than a fake.
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    public string DbPath { get; }

    public DbContextOptions<AppDbContext> Options { get; }

    public SqliteTestDatabase(params IInterceptor[] interceptors)
    {
        DbPath = Path.Combine(Path.GetTempPath(), $"nexgrades-test-{Guid.NewGuid():N}.db");

        var builder = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={DbPath}");
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        Options = builder.Options;

        using var context = new AppDbContext(Options);
        context.Database.Migrate();
    }

    public AppDbContext CreateContext() => new(Options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            File.Delete(DbPath);
        }
        catch (IOException)
        {
            // Best-effort cleanup; a lingering temp file doesn't fail the test run.
        }
    }
}

/// <summary>Settable <see cref="IClock"/> test double, for deterministic time-dependent assertions.</summary>
internal sealed class FixedClock(DateTime now) : IClock
{
    public DateTime Now { get; set; } = now;
}
