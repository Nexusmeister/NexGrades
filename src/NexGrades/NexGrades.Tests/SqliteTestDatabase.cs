using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NexGrades.Data;

namespace NexGrades.Tests;

/// <summary>
/// A real SQLite database file (migrated, on disk, not in-memory), so tests exercise the actual provider
/// (decimal-as-TEXT mapping, string-enum conversions, unique indexes, transactions) rather than a fake.
/// <para>
/// Pooling is off so each context releases its file handle on dispose and <see cref="Dispose"/> can delete the file
/// without <c>SqliteConnection.ClearAllPools</c>. That call is process-wide: run from one test's teardown it closes
/// native handles that another, parallel test is just opening, which surfaced as intermittent
/// <c>ObjectDisposedException: SQLitePCL.sqlite3</c> failures.
/// </para>
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    public string DbPath { get; }

    public DbContextOptions<AppDbContext> Options { get; }

    public SqliteTestDatabase(params IInterceptor[] interceptors)
    {
        DbPath = Path.Combine(Path.GetTempPath(), $"nexgrades-test-{Guid.NewGuid():N}.db");

        var builder = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={DbPath};Pooling=False");
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        Options = builder.Options;

        using var context = new AppDbContext(Options);
        context.Database.Migrate();
    }

    public AppDbContext CreateContext() => new(Options);

    /// <summary>
    /// A factory over this database, for the Domain services, which open their own short-lived context per
    /// operation. Assert against a fresh <see cref="CreateContext"/> afterwards: a context that seeded the
    /// data still tracks the old values and would not see the service's writes.
    /// </summary>
    public IDbContextFactory<AppDbContext> CreateContextFactory() => new TestDbContextFactory(Options);

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }

    public void Dispose()
    {
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
