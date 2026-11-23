using System.Diagnostics.CodeAnalysis;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NexGrades.App.Infrastructure;
using NexGrades.App.Localization;
using NexGrades.App.Updates;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Settings;

/// <summary>
/// The "Updates" section of the settings page: the running version, the two update preferences, "Check for updates
/// now" and, once an update is downloaded, "Restart now". A preference is saved as soon as it is changed. In an app
/// that cannot update itself (development, Aspire) the section says so and the check does nothing.
/// Exposed as <c>SettingsViewModel.Updates</c>.
/// </summary>
public sealed partial class UpdateSettingsViewModel : ObservableObject
{
    private readonly UpdateCoordinator _coordinator;
    private readonly IUserSettingsStore _settingsStore;
    private readonly UpdateNoticeViewModel _notice;
    private readonly ISnackbarService _snackbar;
    private readonly ILogger<UpdateSettingsViewModel> _logger;
    private readonly Action<Action> _dispatch;

    // Set while LoadAsync applies stored values, so that does not write them straight back.
    private bool _loading;

    // True while StatusText shows the coordinator's download state rather than a check result, so it is cleared when
    // that state ends (for example a failed download).
    private bool _statusFromState;

    /// <summary>Whether the app checks for updates shortly after it starts. Saved when changed.</summary>
    [ObservableProperty]
    private bool _autoCheckForUpdates = true;

    /// <summary>Whether beta releases are offered as updates. Saved when changed; turning it off never downgrades.</summary>
    [ObservableProperty]
    private bool _receiveBetaUpdates;

    /// <summary>True while "Check for updates now" runs.</summary>
    [ObservableProperty]
    private bool _isChecking;

    /// <summary>True when an update is downloaded, so "Restart now" applies.</summary>
    [ObservableProperty]
    private bool _isReadyToInstall;

    /// <summary>The result of the last check or the download state; empty when there is nothing to say.</summary>
    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>Creates the section; it follows the coordinator's state on the UI thread.</summary>
    /// <param name="coordinator">Holds the update state and runs checks.</param>
    /// <param name="settingsStore">Reads and saves the update preferences.</param>
    /// <param name="notice">The window's update notice, whose "Restart now" this section reuses.</param>
    /// <param name="snackbar">Reports a preference that could not be saved.</param>
    /// <param name="logger">Logs failures without settings content.</param>
    public UpdateSettingsViewModel(
        UpdateCoordinator coordinator,
        IUserSettingsStore settingsStore,
        UpdateNoticeViewModel notice,
        ISnackbarService snackbar,
        ILogger<UpdateSettingsViewModel> logger)
        : this(coordinator, settingsStore, notice, snackbar, logger, DispatchToUi)
    {
    }

    /// <summary>Creates the section with its own dispatcher, for tests.</summary>
    /// <param name="coordinator">Holds the update state and runs checks.</param>
    /// <param name="settingsStore">Reads and saves the update preferences.</param>
    /// <param name="notice">The window's update notice, whose "Restart now" this section reuses.</param>
    /// <param name="snackbar">Reports a preference that could not be saved.</param>
    /// <param name="logger">Logs failures without settings content.</param>
    /// <param name="dispatch">Runs the given action on the UI thread.</param>
    internal UpdateSettingsViewModel(
        UpdateCoordinator coordinator,
        IUserSettingsStore settingsStore,
        UpdateNoticeViewModel notice,
        ISnackbarService snackbar,
        ILogger<UpdateSettingsViewModel> logger,
        Action<Action> dispatch)
    {
        ArgumentNullException.ThrowIfNull(coordinator);

        _coordinator = coordinator;
        _settingsStore = settingsStore;
        _notice = notice;
        _snackbar = snackbar;
        _logger = logger;
        _dispatch = dispatch;

        coordinator.StateChanged += (_, _) => _dispatch(ApplyState);
        ApplyState();
    }

    /// <summary>"Version {0}" with the running version.</summary>
    public string VersionText => TextResources.Format("Settings_Updates_Version_Format", _coordinator.CurrentVersionText);

    /// <summary>Whether this app can update itself; the check and the preferences only matter when it can.</summary>
    public bool IsUpdaterAvailable => _coordinator.IsAvailable;

    /// <summary>Closes the app to install the downloaded update and starts it again; the same action as the window's notice.</summary>
    public IAsyncRelayCommand RestartNowCommand => _notice.RestartNowCommand;

    /// <summary>Reads the stored preferences into the toggles; called when the settings page opens.</summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Unreadable settings leave the toggles as they are; the page must still open.")]
    public async Task LoadAsync()
    {
        UserSettings settings;
        try
        {
            settings = await _settingsStore.LoadAsync();
        }
        catch (Exception ex)
        {
            LogLoadFailed(ex.GetType().Name);
            return;
        }

        _loading = true;
        try
        {
            AutoCheckForUpdates = settings.AutoCheckForUpdates;
            ReceiveBetaUpdates = settings.ReceiveBetaUpdates;
        }
        finally
        {
            _loading = false;
        }

        ApplyState();
    }

    /// <summary>Checks for updates now and shows the result; an update found is downloaded in the background.</summary>
    [RelayCommand]
    private async Task CheckNowAsync()
    {
        if (!_coordinator.IsAvailable)
        {
            ShowResult(TextResources.Text("Settings_Updates_NotInstalled"));
            return;
        }

        IsChecking = true;
        try
        {
            var outcome = await _coordinator.CheckNowAsync();
            switch (outcome)
            {
                case UpdateCheckOutcome.NotInstalled:
                    ShowResult(TextResources.Text("Settings_Updates_NotInstalled"));
                    break;
                case UpdateCheckOutcome.UpToDate:
                    ShowResult(TextResources.Text("Settings_Updates_UpToDate"));
                    break;
                case UpdateCheckOutcome.Offline:
                    ShowResult(TextResources.Text("Settings_Updates_Offline"));
                    break;
                default:
                    ApplyState();
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            ShowResult(TextResources.Text("Settings_Updates_Offline"));
        }
        finally
        {
            IsChecking = false;
        }
    }

    partial void OnAutoCheckForUpdatesChanged(bool value)
    {
        if (!_loading)
        {
            _ = SaveAsync(settings => settings with { AutoCheckForUpdates = value });
        }
    }

    partial void OnReceiveBetaUpdatesChanged(bool value)
    {
        if (!_loading)
        {
            _ = SaveAsync(settings => settings with { ReceiveBetaUpdates = value });
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A preference that cannot be saved is reported to the user; it must not crash the page.")]
    private async Task SaveAsync(Func<UserSettings, UserSettings> change)
    {
        try
        {
            _ = await _settingsStore.UpdateAsync(change);
        }
        catch (Exception ex)
        {
            LogSaveFailed(ex.GetType().Name);
            _snackbar.Show(
                TextResources.Text("Common_Error"),
                TextResources.Text("Settings_Updates_SaveFailed"),
                ControlAppearance.Caution,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Takes over what the coordinator reports; a finished check's own result stays until the state moves on.</summary>
    private void ApplyState()
    {
        IsReadyToInstall = _coordinator.State == UpdateState.ReadyToInstall;
        if (!_coordinator.IsAvailable)
        {
            ShowResult(TextResources.Text("Settings_Updates_NotInstalled"));
        }
        else if (StateText() is { } text)
        {
            StatusText = text;
            _statusFromState = true;
        }
        else if (_statusFromState)
        {
            ShowResult(string.Empty);
        }

        OnPropertyChanged(nameof(IsUpdaterAvailable));
    }

    private void ShowResult(string text)
    {
        StatusText = text;
        _statusFromState = false;
    }

    /// <summary>The text for a download in progress or a ready update, or <see langword="null"/> in any other state.</summary>
    private string? StateText()
    {
        var version = _coordinator.ReadyVersion;
        if (version is null)
        {
            return null;
        }

        return _coordinator.State switch
        {
            UpdateState.Downloading => TextResources.Format("Settings_Updates_Downloading_Format", version),
            UpdateState.ReadyToInstall => TextResources.Format("Shell_UpdateNotice_Ready_Format", version),
            _ => null,
        };
    }

    [SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "Raised from a background check or download; the bound state must change on the UI thread, and nothing waits for it.")]
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "The update settings could not be read: {ExceptionType}")]
    private partial void LogLoadFailed(string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "An update setting could not be saved: {ExceptionType}")]
    private partial void LogSaveFailed(string exceptionType);
}
