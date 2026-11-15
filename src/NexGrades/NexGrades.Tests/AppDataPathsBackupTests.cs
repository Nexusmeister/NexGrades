using NexGrades.Common.Storage;

namespace NexGrades.Tests;

public sealed class AppDataPathsBackupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"nexgrades-paths-{Guid.NewGuid():N}");

    public void Dispose()
    {
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
    public void SettingsFilePath_IsSettingsJsonInsideDataDirectory()
    {
        var paths = new AppDataPaths(Path.Combine(_root, "data"));

        Assert.Equal(Path.Combine(paths.DataDirectory, "settings.json"), paths.SettingsFilePath);
    }

    [Fact]
    public void BackupDirectory_IsBackupsFolderInsideDataDirectory()
    {
        var paths = new AppDataPaths(Path.Combine(_root, "data"));

        Assert.Equal(Path.Combine(paths.DataDirectory, "backups"), paths.BackupDirectory);
    }

    [Fact]
    public void EnsureCreated_CreatesNeitherSettingsFileNorBackupDirectory()
    {
        var paths = new AppDataPaths(Path.Combine(_root, "data"));

        paths.EnsureCreated();

        Assert.True(Directory.Exists(paths.DataDirectory));
        Assert.False(File.Exists(paths.SettingsFilePath));
        Assert.False(Directory.Exists(paths.BackupDirectory));
    }
}
