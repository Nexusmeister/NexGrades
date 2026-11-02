using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexGrades.Data;
using NexGrades.Data.Services;

namespace NexGrades.Tests;

public class DatabaseMigrationServiceLoggingTests
{
    private sealed record Entry(LogLevel Level, string Message, Exception? Exception);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<Entry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            Entries.Add(new Entry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed class FailingFactory : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => throw new InvalidOperationException("ZZ-SECRET factory failure");
    }

    /// <summary>A database file with no schema at all, so every migration is pending.</summary>
    private sealed class UnmigratedDatabase : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"nexgrades-test-{Guid.NewGuid():N}.db");

        public IDbContextFactory<AppDbContext> Factory { get; }

        public UnmigratedDatabase()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_path}").Options;
            Factory = new OptionsFactory(options);
        }

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try
            {
                File.Delete(_path);
            }
            catch (IOException)
            {
                // Best-effort cleanup of the temp file.
            }
        }

        private sealed class OptionsFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
        {
            public AppDbContext CreateDbContext() => new(options);
        }
    }

    [Fact]
    public async Task RunAsync_LogsMigrationLines_OnFreshDatabase()
    {
        using var database = new UnmigratedDatabase();
        var logger = new CapturingLogger<DatabaseMigrationService>();

        await new DatabaseMigrationService(database.Factory, logger).RunAsync();

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.StartsWith("Checking database migrations; ", StringComparison.Ordinal) && !e.Message.Contains("; 0 pending", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message == "Database migrations applied");
        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task RunAsync_LogsUpToDateWithoutApplying_WhenNothingIsPending()
    {
        using var db = new SqliteTestDatabase();
        var logger = new CapturingLogger<DatabaseMigrationService>();

        await new DatabaseMigrationService(db.CreateContextFactory(), logger).RunAsync();

        Assert.Contains(logger.Entries, e => e.Message == "Checking database migrations; 0 pending");
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Debug && e.Message == "Database already up to date");
        Assert.DoesNotContain(logger.Entries, e => e.Message == "Database migrations applied");
    }

    [Fact]
    public async Task RunAsync_CreatesNoSchoolYear()
    {
        using var db = new SqliteTestDatabase();
        var logger = new CapturingLogger<DatabaseMigrationService>();

        await new DatabaseMigrationService(db.CreateContextFactory(), logger).RunAsync();

        await using var verify = db.CreateContext();
        Assert.Empty(await verify.SchoolYears.ToListAsync());
        Assert.DoesNotContain(logger.Entries, e => e.Message == "Created an Active school year");
    }

    [Fact]
    public async Task RunAsync_LogsErrorWithExceptionAndRethrows_WhenDatabaseCannotBeOpened()
    {
        var logger = new CapturingLogger<DatabaseMigrationService>();
        var service = new DatabaseMigrationService(new FailingFactory(), logger);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunAsync());

        var entry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.Equal("Database migration failed", entry.Message);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }
}
