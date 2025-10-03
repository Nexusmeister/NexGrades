using Microsoft.Win32;
using NexGrades.Common.Services;

namespace NexGrades.App.Infrastructure;

public class WindowsFileSystemDialogService : IFileSystemDialogService
{
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