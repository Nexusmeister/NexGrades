using System.IO;
using Microsoft.Extensions.Configuration;
using NexGrades.App.Infrastructure;
using NexGrades.App.Storage;

namespace NexGrades.App.Tests;

public sealed class DatabaseBackupLocationTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private static IConfiguration Configuration(string? backupDirectory = null)
    {
        var values = new Dictionary<string, string?>();
        if (backupDirectory is not null)
        {
            values[DatabaseBackupLocation.ConfigurationKey] = backupDirectory;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private AppDataPaths Paths() => new(Path.Combine(_folder.Path, "data"));

    [Fact]
    public void Resolve_UsesTheBackupFolderOfTheDataDirectory_ForTheDefaultDatabase()
    {
        var paths = Paths();
        var location = new DatabaseLocation($"Data Source={paths.DatabaseFilePath}", IsOverride: false);

        var result = DatabaseBackupLocation.Resolve(Configuration(), location, paths);

        Assert.Equal(paths.BackupDirectory, result.Directory);
    }

    [Fact]
    public void Resolve_UsesABackupsFolderNextToTheDatabase_ForAnOverriddenDatabase()
    {
        var databaseFolder = Path.Combine(_folder.Path, "dev");
        var location = new DatabaseLocation($"Data Source={Path.Combine(databaseFolder, "app.db")}", IsOverride: true);

        var result = DatabaseBackupLocation.Resolve(Configuration(), location, Paths());

        Assert.Equal(Path.Combine(databaseFolder, "backups"), result.Directory);
        Assert.NotEqual(Paths().BackupDirectory, result.Directory);
    }

    [Fact]
    public void Resolve_UsesTheConfiguredFolder_OverEverythingElse()
    {
        var configured = Path.Combine(_folder.Path, "elsewhere");
        var location = new DatabaseLocation($"Data Source={Path.Combine(_folder.Path, "dev", "app.db")}", IsOverride: true);

        var result = DatabaseBackupLocation.Resolve(Configuration(configured), location, Paths());

        Assert.Equal(configured, result.Directory);
    }

    [Fact]
    public void Resolve_UsesTheConfiguredFolder_ForTheDefaultDatabaseToo()
    {
        var configured = Path.Combine(_folder.Path, "elsewhere");
        var location = new DatabaseLocation("Data Source=ignored.db", IsOverride: false);

        var result = DatabaseBackupLocation.Resolve(Configuration(configured), location, Paths());

        Assert.Equal(configured, result.Directory);
    }

    [Fact]
    public void Resolve_MakesAConfiguredRelativeFolderAbsolute()
    {
        var location = new DatabaseLocation("Data Source=ignored.db", IsOverride: false);

        var result = DatabaseBackupLocation.Resolve(Configuration("relative-backups"), location, Paths());

        Assert.Equal(Path.GetFullPath("relative-backups"), result.Directory);
    }

    [Fact]
    public void Resolve_IgnoresABlankConfiguredFolder()
    {
        var paths = Paths();
        var location = new DatabaseLocation("Data Source=ignored.db", IsOverride: false);

        var result = DatabaseBackupLocation.Resolve(Configuration("   "), location, paths);

        Assert.Equal(paths.BackupDirectory, result.Directory);
    }

    [Theory]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=scratch;Mode=Memory")]
    [InlineData("Data Source=file:scratch?mode=memory&cache=shared")]
    [InlineData("Data Source=")]
    public void Resolve_HasNoFolder_ForADatabaseThatIsNotAFile(string connectionString)
    {
        var location = new DatabaseLocation(connectionString, IsOverride: true);

        var result = DatabaseBackupLocation.Resolve(Configuration(), location, Paths());

        Assert.Null(result.Directory);
    }

    [Fact]
    public void Resolve_HasNoFolder_ForAnUnparsableConnectionString()
    {
        var location = new DatabaseLocation("this is not a connection string", IsOverride: true);

        var result = DatabaseBackupLocation.Resolve(Configuration(), location, Paths());

        Assert.Null(result.Directory);
    }

    [Fact]
    public void Resolve_CreatesNothing()
    {
        var location = new DatabaseLocation($"Data Source={Path.Combine(_folder.Path, "dev", "app.db")}", IsOverride: true);

        _ = DatabaseBackupLocation.Resolve(Configuration(), location, Paths());

        Assert.Empty(Directory.EnumerateFileSystemEntries(_folder.Path));
    }
}
