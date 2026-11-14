using NexGrades.Domain.Time;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Core.YearLock;

/// <summary>Shows <see cref="UnlockYearDialog"/> in the shell dialog host.</summary>
/// <param name="contentDialogService">Hosts the dialog on top of the current page.</param>
/// <param name="clock">Source of "now" for the dialog's duration choices and validation.</param>
public sealed class UnlockYearDialogService(IContentDialogService contentDialogService, IClock clock) : IUnlockYearDialogService
{
    /// <inheritdoc />
    public async Task<UnlockRequest?> AskAsync(string schoolYearLabel, CancellationToken cancellationToken = default)
    {
        var viewModel = new UnlockYearDialogViewModel(schoolYearLabel, clock);
        var dialog = new UnlockYearDialog(contentDialogService.GetDialogHostEx(), viewModel);

        var result = await contentDialogService.ShowAsync(dialog, cancellationToken);

        return result == ContentDialogResult.Primary
            ? viewModel.TryCreateRequest()
            : null;
    }
}
