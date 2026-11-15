using Wpf.Ui;

namespace NexGrades.App.Core;

/// <summary>
/// The leave guard of a form page: asks before unsaved input is discarded, and stays quiet once the page itself
/// decided to leave. Shared so every form page behaves the same way (see <see cref="DiscardChangesDialog"/>).
/// </summary>
/// <param name="dialogService">Shows the confirmation.</param>
public sealed class LeaveGuard(IContentDialogService dialogService)
{
    private bool _allowLeave;

    /// <summary>Re-arms the guard; call when the page is shown.</summary>
    public void Reset() => _allowLeave = false;

    /// <summary>
    /// Whether sidebar navigation or the back button may leave the page. True when the page already decided to leave
    /// or nothing is unsaved; otherwise the user is asked.
    /// </summary>
    /// <param name="hasUnsavedChanges">Whether the form holds unsaved input.</param>
    /// <param name="cancellationToken">Cancels the dialog.</param>
    public async Task<bool> CanLeaveAsync(bool hasUnsavedChanges, CancellationToken cancellationToken = default)
    {
        if (_allowLeave || !hasUnsavedChanges)
        {
            return true;
        }

        return await DiscardChangesDialog.ConfirmAsync(dialogService, DiscardChangesDialog.UnsavedChangesMessage, cancellationToken);
    }

    /// <summary>Goes back without the guard asking a second time; re-arms the guard if going back fails.</summary>
    /// <param name="goBack">Navigates back; returns whether it did.</param>
    public void LeaveWithoutPrompt(Func<bool> goBack)
    {
        ArgumentNullException.ThrowIfNull(goBack);

        _allowLeave = true;
        if (!goBack())
        {
            _allowLeave = false;
        }
    }

    /// <summary>Goes back on a cancel, asking first when the form holds unsaved input.</summary>
    /// <param name="hasUnsavedChanges">Whether the form holds unsaved input.</param>
    /// <param name="goBack">Navigates back; returns whether it did.</param>
    /// <param name="cancellationToken">Cancels the dialog.</param>
    public async Task CancelAsync(bool hasUnsavedChanges, Func<bool> goBack, CancellationToken cancellationToken = default)
    {
        if (await CanLeaveAsync(hasUnsavedChanges, cancellationToken))
        {
            LeaveWithoutPrompt(goBack);
        }
    }
}
