using System.IO;

namespace NexGrades.App.Tests;

/// <summary>A unique temp folder that is removed on dispose.</summary>
internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nexgrades-log-test-{Guid.NewGuid():N}");

    public TempFolder() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temp folder.
        }
    }
}
