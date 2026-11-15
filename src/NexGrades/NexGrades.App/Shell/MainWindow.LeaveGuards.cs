using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;

namespace NexGrades.App.Shell;

/// <summary>
/// The parts of <see cref="MainWindow"/> that keep unsaved input from being lost: the browser-style back gestures
/// (Alt+Left, the mouse back button) and closing the window. They ask the current page through
/// <see cref="Core.PageNavigator"/>, the same check as the sidebar and the back button.
/// </summary>
public partial class MainWindow
{
    /// <inheritdoc />
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Alt+Left and the mouse back button raise BrowseBack, which would navigate back without the guard.
        CommandManager.AddPreviewExecutedHandler(this, OnBrowseBackExecuting);
    }

    private void OnBrowseBackExecuting(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command != NavigationCommands.BrowseBack)
        {
            return;
        }

        e.Handled = true;

        // The guard runs the back navigation itself once the page agrees; false means no page needs asking.
        if (!_pageNavigator.TryGuardBack(RootNavigation.GoBack))
        {
            _ = RootNavigation.GoBack();
        }
    }

    /// <summary>
    /// Asks the current page before the window closes. When it must ask, the close is cancelled and issued again once
    /// the user agrees; the flag the navigator keeps makes that second close, and a close after "Restart now" has
    /// asked already, go through without a second prompt. A confirmed close still ends in <c>OnClosed</c>, so the
    /// pending update is applied on exit.
    /// </summary>
    /// <param name="e">Carries the cancel flag.</param>
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (e.Cancel)
        {
            return;
        }

        var confirmation = _pageNavigator.ConfirmExitAsync();
        if (confirmation.IsCompletedSuccessfully && confirmation.Result)
        {
            return;
        }

        e.Cancel = true;
        _ = CloseWhenConfirmedAsync(confirmation);
    }

    [SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "The task comes from the navigator's own leave check and is awaited on the UI thread without blocking.")]
    private async Task CloseWhenConfirmedAsync(Task<bool> confirmation)
    {
        if (await confirmation)
        {
            Close();
        }
    }
}
