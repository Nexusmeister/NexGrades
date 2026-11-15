namespace NexGrades.App.Features.Students;

/// <summary>Asks which notes to export. An interface so view models stay free of WPF dialogs.</summary>
public interface IExportNotesDialogService
{
    /// <summary>
    /// Shows the dialog with the given default range. Returns the choice, or <see langword="null"/> when the
    /// user cancels.
    /// </summary>
    /// <param name="defaultFrom">The initial start of the range.</param>
    /// <param name="defaultTo">The initial end of the range.</param>
    /// <param name="cancellationToken">Closes the dialog when cancelled.</param>
    Task<ExportNotesRequest?> AskAsync(DateOnly defaultFrom, DateOnly defaultTo, CancellationToken cancellationToken = default);
}
