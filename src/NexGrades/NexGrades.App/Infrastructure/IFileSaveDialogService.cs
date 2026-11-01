namespace NexGrades.App.Infrastructure;

/// <summary>Asks the user where to save a file. An interface so view models stay free of WPF dialogs.</summary>
public interface IFileSaveDialogService
{
    /// <summary>Shows a save dialog for an .xlsx file. Returns the chosen path, or <see langword="null"/> when the user cancels.</summary>
    /// <param name="suggestedFileName">The file name the dialog starts with.</param>
    string? SelectSaveFile(string suggestedFileName);
}
