using NexGrades.App.Localization;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace NexGrades.App.Core;

/// <summary>
/// The "discard unsaved changes" confirmation that pages with a form show before they are left, so every such page
/// words and answers it the same way.
/// </summary>
public static class DiscardChangesDialog
{
    /// <summary>The text for a page whose form holds unsaved input, in the current UI language.</summary>
    public static string UnsavedChangesMessage => TextResources.Text("Core_DiscardChanges_Message");

    /// <summary>Asks whether unsaved input may be discarded.</summary>
    /// <param name="dialogService">Shows the dialog in the main window.</param>
    /// <param name="content">The question; usually <see cref="UnsavedChangesMessage"/>.</param>
    /// <param name="cancellationToken">Cancels the dialog.</param>
    /// <returns><see langword="true"/> when the user chose to discard.</returns>
    public static async Task<bool> ConfirmAsync(IContentDialogService dialogService, string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dialogService);

        var confirmation = await dialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = TextResources.Text("Core_DiscardChanges_Title"),
                Content = content,
                PrimaryButtonText = TextResources.Text("Common_Discard"),
                CloseButtonText = TextResources.Text("Common_KeepEditing"),
            },
            cancellationToken);

        return confirmation == ContentDialogResult.Primary;
    }
}
