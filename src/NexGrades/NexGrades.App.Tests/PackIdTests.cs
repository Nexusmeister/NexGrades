using System.IO;
using System.Reflection;
using NexGrades.App.Storage;

namespace NexGrades.App.Tests;

/// <summary>
/// Velopack installs to <c>%LOCALAPPDATA%\&lt;packId&gt;</c> and deletes that folder on uninstall, so the pack id must
/// never lead to the data folder with the database, its backups and the logs.
/// </summary>
public class PackIdTests
{
    private static string PackId() => typeof(App).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "VelopackPackId")
        .Value ?? string.Empty;

    private static string InstallFolder() =>
        Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), PackId()));

    [Fact]
    public void PackId_IsNotEmptyAndNotNexGrades()
    {
        Assert.False(string.IsNullOrWhiteSpace(PackId()));
        Assert.NotEqual("NexGrades", PackId().Trim(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void InstallFolder_IsNotTheDataDirectory()
    {
        var data = Path.GetFullPath(AppDataPaths.CreateDefault().DataDirectory);

        Assert.NotEqual(data, InstallFolder(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void InstallFolder_IsNotAParentOfTheDataDirectory()
    {
        var data = Path.GetFullPath(AppDataPaths.CreateDefault().DataDirectory);
        var install = InstallFolder().TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        Assert.False(data.StartsWith(install, StringComparison.OrdinalIgnoreCase), "Uninstalling would delete the data folder.");
    }

    [Fact]
    public void InstallFolder_IsNotInsideTheDataDirectory()
    {
        var data = Path.GetFullPath(AppDataPaths.CreateDefault().DataDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        Assert.False(InstallFolder().StartsWith(data, StringComparison.OrdinalIgnoreCase));
    }
}
