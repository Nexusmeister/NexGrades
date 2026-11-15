using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace NexGrades.App.Infrastructure;

/// <summary>Windows implementation of <see cref="IFolderOpener"/> that starts Explorer on the folder.</summary>
public sealed class ExplorerFolderOpener : IFolderOpener
{
    /// <inheritdoc />
    public void Open(string path)
    {
        try
        {
            // Explorer gets the path as one argument, so spaces and special characters need no quoting.
            // An absolute path keeps the search path (current directory, PATH) from deciding which program runs.
            var explorerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            var startInfo = new ProcessStartInfo(explorerPath) { UseShellExecute = false };
            startInfo.ArgumentList.Add(path);
            using var process = Process.Start(startInfo);
        }
        catch (Win32Exception ex)
        {
            throw new IOException("The file browser could not be started.", ex);
        }
    }
}
