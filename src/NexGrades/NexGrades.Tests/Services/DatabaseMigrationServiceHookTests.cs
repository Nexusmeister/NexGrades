using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using NexGrades.Data;
using NexGrades.Data.Services;
using NexGrades.Tests.TestSupport;

namespace NexGrades.Tests.Services;

public sealed class DatabaseMigrationServiceHookTests : IDisposable
{
    private const string EarlierMigration = "20261027190646_AddNoteArchivedOn";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"nexgrades-hook-{Guid.NewGuid():N}.db");
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly CapturingLogger<DatabaseMigrationService> _logger = new();

    public DatabaseMigrationServiceHookTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(SqliteTestDatabase.ConnectionStringFor(_path)).Options;
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temp file.
        }
    }

    private DatabaseMigrationService CreateService() => new(new OptionsFactory(_options), _logger);

    private void MigrateTo(string? migration)
    {
        using var context = new AppDbContext(_options);
        context.GetService<IMigrator>().Migrate(migration);
    }

    private async Task<(List<string> Applied, List<string> Pending)> ReadMigrationsAsync()
    {
        await using var context = new AppDbContext(_options);
        return ((await context.Database.GetAppliedMigrationsAsync()).ToList(), (await context.Database.GetPendingMigrationsAsync()).ToList());
    }

    [Fact]
    public async Task RunAsync_CallsHookOnceBeforeMigrating_WhenDatabaseIsPartlyMigrated()
    {
        MigrateTo(EarlierMigration);
        var (appliedBefore, _) = await ReadMigrationsAsync();
        var calls = 0;
        List<string>? appliedInHook = null;

        await CreateService().RunAsync(async _ =>
        {
            calls++;
            appliedInHook = (await ReadMigrationsAsync()).Applied;
        });

        var (applied, pending) = await ReadMigrationsAsync();
        Assert.Equal(1, calls);
        Assert.Equal(appliedBefore, appliedInHook);
        Assert.Empty(pending);
        Assert.True(applied.Count > appliedBefore.Count);
    }

    [Fact]
    public async Task RunAsync_DoesNotCallHook_WhenDatabaseIsUpToDate()
    {
        MigrateTo(null);
        var calls = 0;

        await CreateService().RunAsync(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task RunAsync_DoesNotCallHook_AndMigrates_OnFreshDatabase()
    {
        var calls = 0;

        await CreateService().RunAsync(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        var (applied, pending) = await ReadMigrationsAsync();
        Assert.Equal(0, calls);
        Assert.NotEmpty(applied);
        Assert.Empty(pending);
    }

    [Fact]
    public async Task RunAsync_RethrowsHookExceptionUnchangedAndMigratesNothing_WhenHookThrows()
    {
        MigrateTo(EarlierMigration);
        var before = await ReadMigrationsAsync();
        var failure = new IOException("backup failed");

        var thrown = await Assert.ThrowsAsync<IOException>(() => CreateService().RunAsync(_ => throw failure));

        Assert.Same(failure, thrown);
        var after = await ReadMigrationsAsync();
        Assert.Equal(before.Applied, after.Applied);
        Assert.Equal(before.Pending, after.Pending);
        Assert.NotEmpty(after.Pending);
    }

    [Fact]
    public async Task RunAsync_LogsNoMigrationFailure_WhenHookThrows()
    {
        MigrateTo(EarlierMigration);

        _ = await Assert.ThrowsAsync<IOException>(() => CreateService().RunAsync(_ => throw new IOException("backup failed")));

        Assert.DoesNotContain(_logger.Entries, e => e.Message == "Database migration failed");
        Assert.DoesNotContain(_logger.Entries, e => e.Level >= LogLevel.Error);
        Assert.DoesNotContain(_logger.Entries, e => e.Message == "Database migrations applied");
    }

    [Fact]
    public async Task RunAsync_LogsMigrationFailure_WhenMigrationFailsAfterHook()
    {
        MigrateTo(EarlierMigration);

        // A leftover table with the name a pending migration creates makes that migration fail.
        await using (var context = new AppDbContext(_options))
        {
            _ = await context.Database.ExecuteSqlRawAsync("CREATE TABLE Assessments (Id INTEGER)");
        }

        _ = await Assert.ThrowsAnyAsync<Exception>(() => CreateService().RunAsync(_ => Task.CompletedTask));

        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Error && e.Message == "Database migration failed");
    }

    [Fact]
    public async Task RunAsync_Migrates_WhenCalledWithoutHookOnPartlyMigratedDatabase()
    {
        MigrateTo(EarlierMigration);

        await CreateService().RunAsync();

        Assert.Empty((await ReadMigrationsAsync()).Pending);
    }

    private sealed class OptionsFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}
