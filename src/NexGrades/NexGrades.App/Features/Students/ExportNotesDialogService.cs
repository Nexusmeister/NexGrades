using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Students;

/// <summary>Shows <see cref="ExportNotesDialog"/> in the shell dialog host.</summary>
/// <param name="contentDialogService">Hosts the dialog on top of the current page.</param>
public sealed class ExportNotesDialogService(IContentDialogService contentDialogService) : IExportNotesDialogService
{
    /// <inheritdoc />
    public async Task<ExportNotesRequest?> AskAsync(
        DateOnly defaultFrom, DateOnly defaultTo, CancellationToken cancellationToken = default)
    {
        var viewModel = new ExportNotesDialogViewModel(defaultFrom, defaultTo);
        var dialog = new ExportNotesDialog(contentDialogService.GetDialogHostEx(), viewModel);

        var result = await contentDialogService.ShowAsync(dialog, cancellationToken);

        return result == ContentDialogResult.Primary
            ? viewModel.TryCreateRequest()
            : null;
    }
}
