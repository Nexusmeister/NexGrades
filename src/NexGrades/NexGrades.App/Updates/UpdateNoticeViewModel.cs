using System.Diagnostics.CodeAnalysis;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NexGrades.App.Core;
using NexGrades.App.Infrastructure;
using NexGrades.App.Localization;

namespace NexGrades.App.Updates;

/// <summary>
/// The notice in the main window that an update is downloaded: "Version {0} will be installed when you close
/// NexGrades.", with a "Restart now" action. Restarting asks the current page first, like leaving it would, so unsaved
/// input is not lost. Shown as <c>MainWindowViewModel.UpdateNotice</c>.
/// </summary>
public sealed partial class UpdateNoticeViewModel : ObservableObject
{
    private readonly UpdateCoordinator _coordinator;
    private readonly PageNavigator _pageNavigator;
    private readonly IAppShutdown _shutdown;
    private readonly Action<Action> _dispatch;

    /// <summary>Whether the notice is shown. The user can close it; it opens again only when another update is ready.</summary>
    [ObservableProperty]
    private bool _isOpen;

    /// <summary>The notice text, formatted for the current culture.</summary>
    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>Creates the notice; it follows the coordinator's state on the UI thread.</summary>
    /// <param name="coordinator">Holds the update state and installs the update on exit.</param>
    /// <param name="pageNavigator">Asks the current page whether it may be left before restarting.</param>
    /// <param name="shutdown">Ends the app so the update is installed.</param>
    public UpdateNoticeViewModel(UpdateCoordinator coordinator, PageNavigator pageNavigator, IAppShutdown shutdown)
        : this(coordinator, pageNavigator, shutdown, DispatchToUi)
    {
    }

    /// <summary>Creates the notice with its own dispatcher, for tests.</summary>
    /// <param name="coordinator">Holds the update state and installs the update on exit.</param>
    /// <param name="pageNavigator">Asks the current page whether it may be left before restarting.</param>
    /// <param name="shutdown">Ends the app so the update is installed.</param>
    /// <param name="dispatch">Runs the given action on the UI thread.</param>
    internal UpdateNoticeViewModel(UpdateCoordinator coordinator, PageNavigator pageNavigator, IAppShutdown shutdown, Action<Action> dispatch)
    {
        ArgumentNullException.ThrowIfNull(coordinator);

        _coordinator = coordinator;
        _pageNavigator = pageNavigator;
        _shutdown = shutdown;
        _dispatch = dispatch;

        coordinator.StateChanged += (_, _) => _dispatch(Refresh);
        Refresh();
    }

    /// <summary>
    /// Leaves the current page if it allows it, then closes the app; the update is installed and the app starts again.
    /// </summary>
    [RelayCommand]
    private async Task RestartNowAsync()
    {
        if (!await _pageNavigator.ConfirmExitAsync())
        {
            return;
        }

        _coordinator.RequestRestartOnExit();
        _shutdown.Shutdown();
    }

    private void Refresh()
    {
        var ready = _coordinator.State == UpdateState.ReadyToInstall && _coordinator.ReadyVersion is not null;
        if (ready)
        {
            Message = TextResources.Format("Shell_UpdateNotice_Ready_Format", _coordinator.ReadyVersion);
        }

        IsOpen = ready;
    }

    [SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "Raised from a background check; the bound state must change on the UI thread, and nothing waits for it.")]
    private static void DispatchToUi(Action action)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }
}
