namespace NexGrades.Common.Services;

/// <summary>
/// Opens a folder picker. It is an interface so that view models can be tested without a Windows dialog;
/// the App supplies the Windows implementation.
/// </summary>
public interface IFileSystemDialogService
{
    /// <summary>Lets the user pick one or more folders, starting in <paramref name="basePath"/>.</summary>
    /// <param name="basePath">The folder the picker opens in. Empty means the picker's default folder.</param>
    /// <returns>The chosen folder paths, or an empty sequence when the user cancels.</returns>
    IEnumerable<string> SelectFolder(string basePath = "");
}
