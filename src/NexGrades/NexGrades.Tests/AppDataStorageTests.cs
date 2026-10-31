using Microsoft.EntityFrameworkCore;
using NexGrades.Common.Storage;

namespace NexGrades.Tests;

public sealed class AppDataStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"nexgrades-storage-{Guid.NewGuid():N}");

    private string NewDir(string name)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Db(string dir) => Path.Combine(dir, "app.db");

    // Minimal bytes the relocator accepts as a NexGrades database.
    private const string Header = "SQLite format 3\0__EFMigrationsHistory";

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temp folder.
        }
    }

    [Fact]
    public void DatabaseFilePath_IsInsideDataDirectory()
    {
        var paths = new AppDataPaths(Path.Combine(_root, "data"));

        Assert.Equal(Path.Combine(paths.DataDirectory, "app.db"), paths.DatabaseFilePath);
    }

    [Fact]
    public void EnsureCreated_CreatesDirectory_WhenMissing_AndIsIdempotent()
    {
        var paths = new AppDataPaths(Path.Combine(_root, "data"));

        paths.EnsureCreated();
        paths.EnsureCreated();

        Assert.True(Directory.Exists(paths.DataDirectory));
    }

    [Fact]
    public void CreateDefault_UsesLocalAppDataNexGrades()
    {
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexGrades");

        Assert.Equal(expected, AppDataPaths.CreateDefault().DataDirectory);
    }

    [Fact]
    public void Relocate_Moves_WhenLegacyDbExistsAndTargetMissing()
    {
        var legacy = NewDir("legacy");
        var target = Db(Path.Combine(_root, "data"));
        File.WriteAllText(Db(legacy), Header + "content");

        var result = new LegacyDatabaseRelocator().Relocate([legacy], target);

        Assert.Equal(LegacyRelocationResult.Moved, result);
        Assert.Equal(Header + "content", File.ReadAllText(target));
        Assert.False(File.Exists(Db(legacy)));
    }

    [Fact]
    public void Relocate_MovesWalAndShm_WithMainFile()
    {
        var legacy = NewDir("legacy");
        var target = Db(NewDir("data"));
        File.WriteAllText(Db(legacy), Header + "main");
        File.WriteAllText(Db(legacy) + "-wal", "wal");
        File.WriteAllText(Db(legacy) + "-shm", "shm");

        new LegacyDatabaseRelocator().Relocate([legacy], target);

        Assert.Equal("wal", File.ReadAllText(target + "-wal"));
        Assert.Equal("shm", File.ReadAllText(target + "-shm"));
        Assert.False(File.Exists(Db(legacy) + "-wal"));
    }

    [Fact]
    public void Relocate_ReturnsTargetExists_AndLeavesBothUntouched_WhenTargetExists()
    {
        var legacy = NewDir("legacy");
        var target = Db(NewDir("data"));
        File.WriteAllText(Db(legacy), Header + "old");
        File.WriteAllText(target, "new");

        var result = new LegacyDatabaseRelocator().Relocate([legacy], target);

        Assert.Equal(LegacyRelocationResult.TargetExists, result);
        Assert.Equal(Header + "old", File.ReadAllText(Db(legacy)));
        Assert.Equal("new", File.ReadAllText(target));
    }

    [Fact]
    public void Relocate_ReturnsNotFound_WhenNoCandidateHasDb()
    {
        var result = new LegacyDatabaseRelocator().Relocate([NewDir("a"), NewDir("b")], Db(Path.Combine(_root, "data")));

        Assert.Equal(LegacyRelocationResult.NotFound, result);
        Assert.False(File.Exists(Db(Path.Combine(_root, "data"))));
    }

    [Fact]
    public void Relocate_PrefersFirstCandidate_WhenBothHaveDb()
    {
        var first = NewDir("first");
        var second = NewDir("second");
        File.WriteAllText(Db(first), Header + "first");
        File.WriteAllText(Db(second), Header + "second");
        var target = Db(Path.Combine(_root, "data"));

        new LegacyDatabaseRelocator().Relocate([first, second], target);

        Assert.Equal(Header + "first", File.ReadAllText(target));
        Assert.True(File.Exists(Db(second)));
    }

    [Fact]
    public void Relocate_DeduplicatesIdenticalCandidates()
    {
        var legacy = NewDir("legacy");
        File.WriteAllText(Db(legacy), Header + "x");

        var result = new LegacyDatabaseRelocator().Relocate([legacy, legacy + Path.DirectorySeparatorChar], Db(Path.Combine(_root, "data")));

        Assert.Equal(LegacyRelocationResult.Moved, result);
    }

    [Fact]
    public void Relocate_ReturnsNotFound_WhenCandidateIsNotANexGradesDatabase()
    {
        var other = NewDir("other");
        File.WriteAllText(Db(other), "not a database");
        var target = Db(Path.Combine(_root, "data"));

        var result = new LegacyDatabaseRelocator().Relocate([other], target);

        Assert.Equal(LegacyRelocationResult.NotFound, result);
        Assert.True(File.Exists(Db(other)));
    }

    [Fact]
    public void Relocate_ReturnsMovedSourceKept_WhenSourceDeleteFails()
    {
        var legacy = NewDir("legacy");
        var target = Db(Path.Combine(_root, "data"));
        File.WriteAllText(Db(legacy), Header + "content");

        // Share Read allows the copy but blocks deletion.
        using var lockStream = new FileStream(Db(legacy), FileMode.Open, FileAccess.Read, FileShare.Read);
        var result = new LegacyDatabaseRelocator().Relocate([legacy], target);

        Assert.Equal(LegacyRelocationResult.MovedSourceKept, result);
        Assert.Equal(Header + "content", File.ReadAllText(target));
        Assert.True(File.Exists(Db(legacy)));
    }

    [Fact]
    public void Relocate_RemovesPartialTarget_WhenCopyFails()
    {
        var legacy = NewDir("legacy");
        var data = NewDir("data");
        var target = Db(data);
        File.WriteAllText(Db(legacy), Header + "main");
        File.WriteAllText(Db(legacy) + "-wal", "wal");
        // A directory squatting on the main target path makes the final copy fail after the sidecar copied.
        Directory.CreateDirectory(target);

        Assert.ThrowsAny<Exception>(() => new LegacyDatabaseRelocator().Relocate([legacy], target));

        Assert.False(File.Exists(target + "-wal"));

        Assert.False(File.Exists(target));
        Assert.True(File.Exists(Db(legacy)));
    }

    [Fact]
    public async Task Relocate_KeepsRealMigratedDatabaseUsable()
    {
        var legacy = NewDir("legacy");
        var target = Db(Path.Combine(_root, "data"));
        using (var db = new SqliteTestDatabase())
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Copy(db.DbPath, Db(legacy));
        }

        new LegacyDatabaseRelocator().Relocate([legacy], target);

        var options = new DbContextOptionsBuilder<NexGrades.Data.AppDbContext>().UseSqlite($"Data Source={target}").Options;
        await using var context = new NexGrades.Data.AppDbContext(options);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }
}
