using System.IO;

namespace NexGrades.App.Infrastructure;

/// <summary>Shows a folder in the system file browser. An abstraction so view models can be tested without launching Explorer.</summary>
public interface IFolderOpener
{
    /// <summary>Opens <paramref name="path"/> in the file browser.</summary>
    /// <param name="path">Full path of an existing folder.</param>
    /// <exception cref="IOException">The file browser could not be started.</exception>
    void Open(string path);
}
