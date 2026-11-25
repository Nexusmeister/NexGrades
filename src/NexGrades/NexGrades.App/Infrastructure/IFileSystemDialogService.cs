namespace NexGrades.App.Infrastructure;

/// <summary>
/// Opens a folder picker. It is an interface so that view models can be tested without a Windows dialog;
/// the App supplies the Windows implementation.
/// </summary>
public interface IFileSystemDialogService
{
    /// <summary>Lets the user select a single folder, starting in <paramref name="basePath"/>.</summary>
    /// <param name="basePath">The folder the picker opens in. Empty means the picker's default folder.</param>
    /// <returns>A sequence with the one chosen folder path, or an empty sequence when the user cancels.</returns>
    IEnumerable<string> SelectFolder(string basePath = "");
}
