using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Infrastructure;
using NexGrades.App.Logging;
using NexGrades.Data;
using NexGrades.Data.Services;
using NexGrades.Domain.Maintenance;

namespace NexGrades.App.Tests;

public sealed class StartupDatabaseMigratorTests : IDisposable
{
    private const string EarlierMigration = "20261027190646_AddNoteArchivedOn";

    private readonly TempFolder _folder = new();
    private readonly string _databasePath;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly FakeUserSettingsStore _settings = new();
    private readonly RecordingLoggerFactory _loggers = new();

    public StartupDatabaseMigratorTests()
    {
        _databasePath = Path.Combine(_folder.Path, "app.db");
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_databasePath};Pooling=False").Options;
    }

    public void Dispose() => _folder.Dispose();

    private string BackupDirectory => Path.Combine(_folder.Path, "backups");

    private StartupDatabaseMigrator Create(string? backupDirectory, bool isOverride = false)
    {
        var factory = new OptionsFactory(_options);
        return new StartupDatabaseMigrator(
            new DatabaseMigrationService(factory, NullLogger<DatabaseMigrationService>.Instance),
            new DatabaseBackupService(factory, new TestClock(new DateTime(2026, 11, 15, 10, 30, 0)), NullLogger<DatabaseBackupService>.Instance),
            new DatabaseBackupLocation(backupDirectory),
            new DatabaseLocation($"Data Source={_databasePath}", isOverride),
            _settings,
            _loggers);
    }

    private void MigrateTo(string? migration)
    {
        using var context = new AppDbContext(_options);
        context.GetService<IMigrator>().Migrate(migration);
    }

    private async Task<(int Applied, int Pending)> CountMigrationsAsync()
    {
        await using var context = new AppDbContext(_options);
        return ((await context.Database.GetAppliedMigrationsAsync()).Count(), (await context.Database.GetPendingMigrationsAsync()).Count());
    }

    private async Task BreakALaterMigrationAsync()
    {
        // A leftover table with the name a pending migration creates makes that migration fail.
        await using var context = new AppDbContext(_options);
        _ = await context.Database.ExecuteSqlRawAsync("CREATE TABLE Assessments (Id INTEGER)");
    }

    [Fact]
    public async Task RunAsync_BacksUpThenMigrates_WhenAnExistingDatabaseIsPending()
    {
        MigrateTo(EarlierMigration);
        _settings.Current = new UserSettings { LastRunVersion = "0.1.0" };

        await Create(BackupDirectory).RunAsync();

        var backup = Assert.Single(Directory.EnumerateFiles(BackupDirectory));
        Assert.Equal("app-20261115-103000-v0.1.0.db", Path.GetFileName(backup));
        Assert.Equal(0, (await CountMigrationsAsync()).Pending);
    }

    [Fact]
    public async Task RunAsync_NamesTheBackupUnknown_WhenNoVersionWasRemembered()
    {
        MigrateTo(EarlierMigration);

        await Create(BackupDirectory).RunAsync();

        Assert.Equal("app-20261115-103000-unknown.db", Path.GetFileName(Assert.Single(Directory.EnumerateFiles(BackupDirectory))));
    }

    [Fact]
    public async Task RunAsync_StillBacksUp_WhenTheSettingsFileCannotBeRead()
    {
        MigrateTo(EarlierMigration);
        _settings.LoadFailure = new IOException("locked");

        await Create(BackupDirectory, isOverride: true).RunAsync();

        Assert.Equal("app-20261115-103000-unknown.db", Path.GetFileName(Assert.Single(Directory.EnumerateFiles(BackupDirectory))));
        Assert.Equal(0, (await CountMigrationsAsync()).Pending);
    }

    [Fact]
    public async Task RunAsync_ThrowsBackupFailedAndLeavesTheDatabaseUnmigrated_WhenTheBackupFails()
    {
        MigrateTo(EarlierMigration);
        var before = await CountMigrationsAsync();
        var blocker = Path.Combine(_folder.Path, "blocked");
        await File.WriteAllTextAsync(blocker, "a file where the folder should be");

        var thrown = await Assert.ThrowsAsync<DatabaseBackupFailedException>(() => Create(blocker).RunAsync());

        Assert.NotNull(thrown.InnerException);
        Assert.Equal(DatabaseBackupFailedException.DefaultMessage, thrown.Message);
        Assert.Equal(before, await CountMigrationsAsync());
        Assert.True(before.Pending > 0);
    }

    [Fact]
    public async Task RunAsync_ThrowsMigrationFailedWithTheBackupPath_WhenTheMigrationFailsAfterTheBackup()
    {
        MigrateTo(EarlierMigration);
        await BreakALaterMigrationAsync();

        var thrown = await Assert.ThrowsAsync<DatabaseMigrationFailedException>(() => Create(BackupDirectory).RunAsync());

        Assert.True(File.Exists(thrown.BackupFilePath));
        Assert.Equal(BackupDirectory, Path.GetDirectoryName(thrown.BackupFilePath));
        Assert.NotNull(thrown.InnerException);
        Assert.DoesNotContain(BackupDirectory, thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ThrowsTheOriginalException_WhenAFreshDatabaseFailsToMigrate()
    {
        // No backup is taken for a new database, so there is nothing to point the user to.
        await using (var context = new AppDbContext(_options))
        {
            _ = await context.Database.ExecuteSqlRawAsync("CREATE TABLE Assessments (Id INTEGER)");
            _ = await context.Database.ExecuteSqlRawAsync("CREATE TABLE Students (Id INTEGER)");
        }

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => Create(BackupDirectory).RunAsync());

        Assert.IsNotType<DatabaseMigrationFailedException>(thrown);
        Assert.IsNotType<DatabaseBackupFailedException>(thrown);
        Assert.False(Directory.Exists(BackupDirectory));
    }

    [Fact]
    public async Task RunAsync_MigratesWithoutBackup_WhenThereIsNoBackupLocation()
    {
        MigrateTo(EarlierMigration);

        await Create(null).RunAsync();

        Assert.Equal(0, (await CountMigrationsAsync()).Pending);
        Assert.False(Directory.Exists(BackupDirectory));
        Assert.Contains(_loggers.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task RunAsync_TakesNoBackup_WhenTheDatabaseIsNew()
    {
        await Create(BackupDirectory).RunAsync();

        Assert.Equal(0, (await CountMigrationsAsync()).Pending);
        Assert.False(Directory.Exists(BackupDirectory));
    }

    [Fact]
    public async Task RunAsync_TakesNoBackup_WhenTheDatabaseIsUpToDate()
    {
        MigrateTo(null);

        await Create(BackupDirectory).RunAsync();

        Assert.False(Directory.Exists(BackupDirectory));
    }

    [Fact]
    public async Task RunAsync_RemembersTheRunningVersion_WhenTheDatabaseIsTheDefaultOne()
    {
        await Create(BackupDirectory).RunAsync();

        Assert.Equal(AppVersion.Current, _settings.Current.LastRunVersion);
        Assert.Equal(1, _settings.UpdateCalls);
    }

    [Fact]
    public async Task RunAsync_DoesNotRewriteTheSettings_WhenTheRunningVersionIsAlreadyRemembered()
    {
        _settings.Current = new UserSettings { LastRunVersion = AppVersion.Current };

        await Create(BackupDirectory).RunAsync();

        Assert.Equal(0, _settings.UpdateCalls);
    }

    [Fact]
    public async Task RunAsync_DoesNotRememberTheVersion_WhenTheDatabaseIsAnOverride()
    {
        await Create(BackupDirectory, isOverride: true).RunAsync();

        Assert.Null(_settings.Current.LastRunVersion);
        Assert.Equal(0, _settings.UpdateCalls);
    }

    [Fact]
    public async Task RunAsync_DoesNotRememberTheVersion_WhenTheMigrationFails()
    {
        MigrateTo(EarlierMigration);
        await BreakALaterMigrationAsync();

        _ = await Assert.ThrowsAsync<DatabaseMigrationFailedException>(() => Create(BackupDirectory).RunAsync());

        Assert.Null(_settings.Current.LastRunVersion);
    }

    [Fact]
    public async Task RunAsync_Starts_WhenRememberingTheVersionFails()
    {
        _settings.UpdateFailure = new IOException("read-only");

        var exception = await Record.ExceptionAsync(() => Create(BackupDirectory).RunAsync());

        Assert.Null(exception);
        Assert.Equal(0, (await CountMigrationsAsync()).Pending);
    }

    [Fact]
    public async Task RunAsync_LogsTheBackupPathOnlyUnderTheLocalOnlyCategory()
    {
        MigrateTo(EarlierMigration);

        await Create(BackupDirectory).RunAsync();

        Assert.Contains(_loggers.Entries, e => e.Category == StartupDiagnostics.LocalOnlyCategory && e.Message.Contains(BackupDirectory, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(_loggers.Entries, e => e.Category != StartupDiagnostics.LocalOnlyCategory && e.Message.Contains(_folder.Path, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class OptionsFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }

    private sealed record RecordedEntry(string Category, LogLevel Level, string Message);

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        public List<RecordedEntry> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Entries);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(string category, List<RecordedEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                ArgumentNullException.ThrowIfNull(formatter);
                entries.Add(new RecordedEntry(category, logLevel, formatter(state, exception)));
            }
        }
    }
}
