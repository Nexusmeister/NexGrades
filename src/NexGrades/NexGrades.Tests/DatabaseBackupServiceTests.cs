using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Maintenance;

namespace NexGrades.Tests;

public sealed class DatabaseBackupServiceTests : IDisposable
{
    private static readonly DateTime _now = new(2026, 11, 15, 10, 30, 0);

    private readonly SqliteTestDatabase _db = new();
    private readonly FixedClock _clock = new(_now);
    private readonly CapturingLogger<DatabaseBackupService> _logger = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"nexgrades-backup-{Guid.NewGuid():N}");

    public void Dispose()
    {
        _db.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temp folder.
        }
    }

    private string BackupDirectory => Path.Combine(_root, "backups");

    private DatabaseBackupService CreateService() => new(_db.CreateContextFactory(), _clock, _logger);

    private async Task SeedAsync(int subjects)
    {
        await using var context = _db.CreateContext();
        for (var i = 0; i < subjects; i++)
        {
            _ = context.Subjects.Add(new Subject { Name = $"Subject {i}", ShortCode = $"S{i}" });
        }

        _ = await context.SaveChangesAsync();
    }

    private static async Task<int> CountSubjectsAsync(string databasePath)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(SqliteTestDatabase.ConnectionStringFor(databasePath)).Options;
        await using var context = new AppDbContext(options);
        return await context.Subjects.CountAsync();
    }

    private string CreateBackupFile(DateTime timestamp, string version = "1.0.0")
    {
        _ = Directory.CreateDirectory(BackupDirectory);
        var path = Path.Combine(BackupDirectory, DatabaseBackupFileName.Create(timestamp, version));
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public async Task CreateBackupAsync_NamesBackupFromClockAndVersion()
    {
        var path = await CreateService().CreateBackupAsync(BackupDirectory, "0.1.1");

        Assert.Equal(Path.Combine(BackupDirectory, "app-20261115-103000-v0.1.1.db"), path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task CreateBackupAsync_CopiesAllRows_AndTheBackupOpens()
    {
        await SeedAsync(3);

        var path = await CreateService().CreateBackupAsync(BackupDirectory, "0.1.1");

        Assert.Equal(3, await CountSubjectsAsync(path));
        await using var context = _db.CreateContext();
        Assert.Equal(3, await context.Subjects.CountAsync());
    }

    [Fact]
    public async Task CreateBackupAsync_LeavesNoTemporaryFile()
    {
        _ = await CreateService().CreateBackupAsync(BackupDirectory, "0.1.1");

        Assert.Empty(Directory.EnumerateFiles(BackupDirectory, "*.tmp"));
        _ = Assert.Single(Directory.EnumerateFiles(BackupDirectory));
    }

    [Fact]
    public async Task CreateBackupAsync_ExcludesUncommittedWrite_FromAnotherConnection()
    {
        await SeedAsync(2);
        await using var writer = _db.CreateContext();
        await using var transaction = await writer.Database.BeginTransactionAsync();
        _ = writer.Subjects.Add(new Subject { Name = "Uncommitted", ShortCode = "UC" });
        _ = await writer.SaveChangesAsync();

        var path = await CreateService().CreateBackupAsync(BackupDirectory, "0.1.1");

        await transaction.RollbackAsync();
        Assert.Equal(2, await CountSubjectsAsync(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.0/../x")]
    public async Task CreateBackupAsync_NamesBackupUnknown_ForMissingOrIllegalVersion(string? version)
    {
        var path = await CreateService().CreateBackupAsync(BackupDirectory, version);

        Assert.Equal("app-20261115-103000-unknown.db", Path.GetFileName(path));
        Assert.Equal(BackupDirectory, Path.GetDirectoryName(path));
    }

    [Fact]
    public async Task CreateBackupAsync_AddsSuffixAndOverwritesNothing_WhenNameIsTakenInTheSameSecond()
    {
        await SeedAsync(1);
        var service = CreateService();
        var first = await service.CreateBackupAsync(BackupDirectory, "0.1.1");
        var firstBytes = await File.ReadAllBytesAsync(first);
        await SeedAsync(2);

        var second = await service.CreateBackupAsync(BackupDirectory, "0.1.1");
        var third = await service.CreateBackupAsync(BackupDirectory, "0.1.1");

        Assert.Equal("app-20261115-103000-v0.1.1-1.db", Path.GetFileName(second));
        Assert.Equal("app-20261115-103000-v0.1.1-2.db", Path.GetFileName(third));
        Assert.Equal(firstBytes, await File.ReadAllBytesAsync(first));
        Assert.Equal(1, await CountSubjectsAsync(first));
        Assert.Equal(3, await CountSubjectsAsync(second));
    }

    [Fact]
    public async Task CreateBackupAsync_DeletesLeftoverTemporaryFile_OfAnEarlierAttempt()
    {
        _ = Directory.CreateDirectory(BackupDirectory);
        var leftover = Path.Combine(BackupDirectory, "app-20261115-103000-v0.1.1.db.tmp");
        var unrelated = Path.Combine(BackupDirectory, "other.tmp");
        await File.WriteAllTextAsync(leftover, "half");
        await File.WriteAllTextAsync(unrelated, "keep");

        var path = await CreateService().CreateBackupAsync(BackupDirectory, "0.1.1");

        Assert.False(File.Exists(leftover));
        Assert.True(File.Exists(unrelated));
        Assert.Equal("app-20261115-103000-v0.1.1.db", Path.GetFileName(path));
    }

    [Fact]
    public async Task CreateBackupAsync_KeepsFiveNewestBackups()
    {
        for (var i = 1; i <= 6; i++)
        {
            _ = CreateBackupFile(new DateTime(2026, 1, i, 8, 0, 0));
        }

        var path = await CreateService().CreateBackupAsync(BackupDirectory, "0.1.1");

        var names = Directory.EnumerateFiles(BackupDirectory).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(DatabaseBackupService.RetainedBackupCount, names.Count);
        Assert.Contains(Path.GetFileName(path), names);
        Assert.DoesNotContain("app-20260101-080000-v1.0.0.db", names);
        Assert.DoesNotContain("app-20260102-080000-v1.0.0.db", names);
    }

    [Fact]
    public async Task CreateBackupAsync_KeepsNewBackup_WhenClockIsBehindExistingBackups()
    {
        for (var i = 1; i <= 6; i++)
        {
            _ = CreateBackupFile(new DateTime(2027, 1, i, 8, 0, 0));
        }

        var path = await CreateService().CreateBackupAsync(BackupDirectory, "0.1.1");

        Assert.True(File.Exists(path));
        Assert.Equal(DatabaseBackupService.RetainedBackupCount, Directory.EnumerateFiles(BackupDirectory).Count());
        Assert.False(File.Exists(Path.Combine(BackupDirectory, "app-20270101-080000-v1.0.0.db")));
        Assert.False(File.Exists(Path.Combine(BackupDirectory, "app-20270102-080000-v1.0.0.db")));
        Assert.True(File.Exists(Path.Combine(BackupDirectory, "app-20270106-080000-v1.0.0.db")));
    }

    [Fact]
    public async Task CreateBackupAsync_Throws_AndLeavesNoDatabaseFile_WhenBackupDirectoryIsAFile()
    {
        _ = Directory.CreateDirectory(_root);
        var blocker = Path.Combine(_root, "backups");
        await File.WriteAllTextAsync(blocker, "not a folder");

        _ = await Assert.ThrowsAnyAsync<IOException>(() => CreateService().CreateBackupAsync(blocker, "0.1.1"));

        Assert.Empty(Directory.EnumerateFiles(_root, "*.db", SearchOption.AllDirectories));
        Assert.Equal("not a folder", await File.ReadAllTextAsync(blocker));
    }

    [Fact]
    public async Task CreateBackupAsync_LogsNoPathOrVersion()
    {
        _ = await CreateService().CreateBackupAsync(BackupDirectory, "0.1.1");

        Assert.NotEmpty(_logger.Entries);
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains(_root, StringComparison.OrdinalIgnoreCase) || e.Message.Contains("0.1.1", StringComparison.Ordinal));
    }

    [Fact]
    public void PruneBackups_DeletesOldestByNameTimestamp_NotByWriteTime()
    {
        var paths = new List<string>();
        for (var i = 1; i <= 7; i++)
        {
            var path = CreateBackupFile(new DateTime(2026, 3, i, 12, 0, 0));

            // The oldest name gets the newest write time, so write time would keep the wrong files.
            File.SetLastWriteTimeUtc(path, new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-i));
            paths.Add(path);
        }

        var deleted = CreateService().PruneBackups(BackupDirectory);

        Assert.Equal(2, deleted);
        Assert.False(File.Exists(paths[0]));
        Assert.False(File.Exists(paths[1]));
        Assert.All(paths.Skip(2), p => Assert.True(File.Exists(p)));
    }

    [Fact]
    public void PruneBackups_IgnoresUnrelatedAndTemporaryFiles()
    {
        for (var i = 1; i <= 6; i++)
        {
            _ = CreateBackupFile(new DateTime(2026, 3, i, 12, 0, 0));
        }

        var kept = new[]
        {
            Path.Combine(BackupDirectory, "app-20200101-000000-v1.0.0.db.tmp"),
            Path.Combine(BackupDirectory, "notes.txt"),
            Path.Combine(BackupDirectory, "app-foo.db"),
            Path.Combine(BackupDirectory, "teacher.db"),
        };
        foreach (var path in kept)
        {
            File.WriteAllText(path, "keep");
        }

        var deleted = CreateService().PruneBackups(BackupDirectory);

        Assert.Equal(1, deleted);
        Assert.All(kept, p => Assert.True(File.Exists(p)));
    }

    [Fact]
    public void PruneBackups_DeletesNothing_WhenFiveOrFewerExist()
    {
        var paths = Enumerable.Range(1, DatabaseBackupService.RetainedBackupCount)
            .Select(i => CreateBackupFile(new DateTime(2026, 3, i, 12, 0, 0)))
            .ToList();

        var deleted = CreateService().PruneBackups(BackupDirectory);

        Assert.Equal(0, deleted);
        Assert.All(paths, p => Assert.True(File.Exists(p)));
    }

    [Fact]
    public void PruneBackups_ReturnsZero_WhenFolderDoesNotExist()
    {
        Assert.Equal(0, CreateService().PruneBackups(BackupDirectory));
        Assert.False(Directory.Exists(BackupDirectory));
    }

    [Fact]
    public void PruneBackups_DoesNotThrow_WhenAFileCannotBeDeleted()
    {
        var paths = Enumerable.Range(1, 6).Select(i => CreateBackupFile(new DateTime(2026, 3, i, 12, 0, 0))).ToList();

        // An open handle without delete sharing makes File.Delete fail on Windows.
        using var locked = new FileStream(paths[0], FileMode.Open, FileAccess.Read, FileShare.None);
        var result = Record.Exception(() => CreateService().PruneBackups(BackupDirectory));

        Assert.Null(result);
        Assert.All(paths.Skip(1), p => Assert.True(File.Exists(p)));
        if (File.Exists(paths[0]))
        {
            Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains(_root, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task CreateBackupAsync_StillReturnsBackup_WhenPruningCannotDeleteAnOldFile()
    {
        var paths = Enumerable.Range(1, 6).Select(i => CreateBackupFile(new DateTime(2026, 3, i, 12, 0, 0))).ToList();

        using var locked = new FileStream(paths[0], FileMode.Open, FileAccess.Read, FileShare.None);
        var path = await CreateService().CreateBackupAsync(BackupDirectory, "0.1.1");

        Assert.True(File.Exists(path));
    }
}
