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
    public void SettingsFilePath_IsAbsoluteOverride_WhenOverrideIsAbsolute()
    {
        var custom = Path.Combine(_root, "elsewhere", "custom.json");

        var paths = new AppDataPaths(Path.Combine(_root, "data"), custom);

        Assert.Equal(custom, paths.SettingsFilePath);
    }

    [Fact]
    public void SettingsFilePath_ResolvesRelativeOverride_ToAbsolutePath()
    {
        var paths = new AppDataPaths(Path.Combine(_root, "data"), Path.Combine("rel", "custom.json"));

        Assert.True(Path.IsPathRooted(paths.SettingsFilePath));
        Assert.Equal(Path.GetFullPath(Path.Combine("rel", "custom.json")), paths.SettingsFilePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SettingsFilePath_IgnoresOverride_WhenEmptyOrWhitespace(string? overridePath)
    {
        var paths = new AppDataPaths(Path.Combine(_root, "data"), overridePath);

        Assert.Equal(Path.Combine(paths.DataDirectory, "settings.json"), paths.SettingsFilePath);
    }

    [Fact]
    public void OtherPaths_StayInDataDirectory_WhenSettingsFileIsOverridden()
    {
        var paths = new AppDataPaths(Path.Combine(_root, "data"), Path.Combine(_root, "elsewhere", "custom.json"));

        Assert.Equal(Path.Combine(paths.DataDirectory, "app.db"), paths.DatabaseFilePath);
        Assert.Equal(Path.Combine(paths.DataDirectory, "logs"), paths.LogDirectory);
        Assert.Equal(Path.Combine(paths.DataDirectory, "backups"), paths.BackupDirectory);
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
