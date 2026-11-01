using Microsoft.Win32;
using NexGrades.Common.Services;

namespace NexGrades.App.Infrastructure;

/// <summary>
/// Windows implementation of <see cref="IFileSystemDialogService"/>, built on the WPF <c>OpenFolderDialog</c>.
/// </summary>
public class WindowsFileSystemDialogService : IFileSystemDialogService
{
    /// <summary>Shows the folder picker with multi-selection enabled. Returns an empty sequence when the user cancels.</summary>
    public IEnumerable<string> SelectFolder(string basePath = "")
    {
        OpenFolderDialog openFolderDialog = new()
        {
            Multiselect = true,
            InitialDirectory = basePath,
        };

        if (openFolderDialog.ShowDialog() != true)
        {
            return [];
        }

        return openFolderDialog.FolderNames.Length == 0 ? [] : openFolderDialog.FolderNames;
    }
}
