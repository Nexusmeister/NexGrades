using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NexGrades.App.Infrastructure;

namespace NexGrades.App.Updates;

/// <summary>
/// Runs the app's update cycle: check the channels the user chose, download a newer version in the background, and
/// install it when the app closes (or restart into it on request). It is the one place that holds the update state,
/// so the window's notice and the settings page show the same thing. When the app is not an installation that can
/// update itself, it stays <see cref="UpdateState.Inactive"/> and does nothing, without disk or network access.
/// </summary>
/// <remarks>
/// Thread-safe: checks run on the thread pool and from the UI. <see cref="StateChanged"/> is raised on whichever thread
/// changed the state, so subscribers marshal to the UI thread themselves. Logs carry versions and channel names only.
/// </remarks>
public sealed partial class UpdateCoordinator
{
    private readonly IUpdateClient _client;
    private readonly IUserSettingsStore _settingsStore;
    private readonly UpdateFeedOptions _options;
    private readonly ILogger<UpdateCoordinator> _logger;
    private readonly Lock _gate = new();

    private UpdateState _state;
    private UpdateCandidate? _update;
    private Task<UpdateCheckOutcome>? _inFlight;
    private bool _restartOnExit;

    /// <summary>Creates the coordinator. It only asks the client whether the app is installed, which touches nothing.</summary>
    /// <param name="client">Checks, downloads and applies updates.</param>
    /// <param name="settingsStore">Holds whether to check automatically and whether betas are wanted.</param>
    /// <param name="options">The per-channel check timeout.</param>
    /// <param name="logger">Logs outcomes with versions and channel names only.</param>
    public UpdateCoordinator(
        IUpdateClient client, IUserSettingsStore settingsStore, IOptions<UpdateFeedOptions> options, ILogger<UpdateCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _settingsStore = settingsStore;
        _options = options.Value;
        _logger = logger;
        _state = IsInstalled() ? UpdateState.Idle : UpdateState.Inactive;
    }

    /// <summary>Raised after <see cref="State"/> or <see cref="ReadyVersion"/> changed, on the thread that changed it.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Where the update cycle is now.</summary>
    public UpdateState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// The version of the update found by the last check: being downloaded while <see cref="State"/> is
    /// <see cref="UpdateState.Downloading"/>, ready while it is <see cref="UpdateState.ReadyToInstall"/>, otherwise
    /// <see langword="null"/>. Culture-neutral, for example <c>0.2.0</c> or <c>0.2.1-beta.3</c>.
    /// </summary>
    public string? ReadyVersion
    {
        get
        {
            lock (_gate)
            {
                return _update?.Version.ToNormalizedString();
            }
        }
    }

    /// <summary>
    /// The version of the running app, for display; culture-neutral. The installed version when there is one, otherwise
    /// the version the build carries.
    /// </summary>
    public string CurrentVersionText => _client.CurrentVersion?.ToNormalizedString() ?? AppVersion.Current;

    /// <summary>Whether this app can update itself; false in development, under Aspire and in tests.</summary>
    public bool IsAvailable => State != UpdateState.Inactive;

    /// <summary>
    /// The check shortly after startup: it first takes up an update that was downloaded before but not installed, then
    /// checks for a newer one if the user left automatic checks on. Failures are logged, not thrown.
    /// </summary>
    /// <param name="cancellationToken">Cancels the check when the app stops.</param>
    public async Task CheckOnStartupAsync(CancellationToken cancellationToken = default)
    {
        if (State == UpdateState.Inactive)
        {
            return;
        }

        TakeUpPendingUpdate();

        var settings = await LoadSettingsAsync(cancellationToken);
        if (!settings.AutoCheckForUpdates)
        {
            LogAutomaticCheckOff();
            return;
        }

        _ = await CheckNowAsync(cancellationToken);
    }

    /// <summary>
    /// Checks the chosen channels now. A caller that arrives while a check runs shares that check. When an update is
    /// already downloading or ready, nothing is checked and the answer is <see cref="UpdateCheckOutcome.UpdateFound"/>.
    /// A found update is downloaded in the background; this method does not wait for the download.
    /// </summary>
    /// <param name="cancellationToken">Cancels the check; with a shared check, the first caller's token applies.</param>
    /// <returns>What the check found.</returns>
    public Task<UpdateCheckOutcome> CheckNowAsync(CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<UpdateCheckOutcome> completion;
        lock (_gate)
        {
            switch (_state)
            {
                case UpdateState.Inactive:
                    return Task.FromResult(UpdateCheckOutcome.NotInstalled);
                case UpdateState.Downloading or UpdateState.ReadyToInstall:
                    return Task.FromResult(UpdateCheckOutcome.UpdateFound);
                case UpdateState.Checking when _inFlight is not null:
                    return _inFlight;
            }

            completion = new TaskCompletionSource<UpdateCheckOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            _inFlight = completion.Task;
            _state = UpdateState.Checking;
        }

        OnStateChanged();
        _ = CompleteCheckAsync(completion, cancellationToken);
        return completion.Task;
    }

    /// <summary>Makes the app start again after the update is installed on exit. Used by "Restart now".</summary>
    public void RequestRestartOnExit()
    {
        lock (_gate)
        {
            _restartOnExit = true;
        }
    }

    /// <summary>
    /// Called while the app exits: if an update is ready, starts the updater, which installs it once this process has
    /// ended (and restarts the app if <see cref="RequestRestartOnExit"/> was called). Synchronous, so it runs before the
    /// exit handler's first <see langword="await"/>.
    /// </summary>
    /// <returns><see langword="true"/> when an update will be installed.</returns>
    public bool ApplyPendingOnExit()
    {
        UpdateCandidate update;
        bool restart;
        lock (_gate)
        {
            if (_state != UpdateState.ReadyToInstall || _update is null)
            {
                return false;
            }

            update = _update;
            restart = _restartOnExit;
        }

        var version = update.Version.ToNormalizedString();
        LogApplyingOnExit(version, restart);
        _client.ApplyOnExit(update, restart);
        return true;
    }

    /// <summary>Waits for the background download started by the last check, if any; for tests.</summary>
    internal Task DownloadCompletion { get; private set; } = Task.CompletedTask;

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The update check is optional; installed state must not depend on whether it worked.")]
    private bool IsInstalled()
    {
        try
        {
            return _client.IsInstalled;
        }
        catch (Exception ex)
        {
            LogInstallStateUnknown(ex.GetType().Name);
            return false;
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A pending update that cannot be read is simply not taken up; the next check finds it again.")]
    private void TakeUpPendingUpdate()
    {
        try
        {
            if (_client.PendingRestart is not { } pending)
            {
                return;
            }

            lock (_gate)
            {
                if (_state != UpdateState.Idle)
                {
                    return;
                }

                _update = pending;
                _state = UpdateState.ReadyToInstall;
            }

            var version = pending.Version.ToNormalizedString();
            LogPendingUpdateFound(version);
            OnStateChanged();
        }
        catch (Exception ex)
        {
            LogPendingUpdateUnreadable(ex);
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Unreadable settings mean the defaults; the check still runs.")]
    private async Task<UserSettings> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _settingsStore.LoadAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSettingsUnreadable(ex.GetType().Name);
            return new UserSettings();
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Every failure of the check must complete the shared task and return the coordinator to Idle.")]
    private async Task CompleteCheckAsync(TaskCompletionSource<UpdateCheckOutcome> completion, CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await RunCheckAsync(cancellationToken);
            completion.SetResult(outcome);
        }
        catch (OperationCanceledException ex)
        {
            SetIdle();
            _ = completion.TrySetCanceled(ex.CancellationToken);
        }
        catch (Exception ex)
        {
            LogCheckFailed(ex);
            SetIdle();
            _ = completion.TrySetException(ex);
        }
    }

    private async Task<UpdateCheckOutcome> RunCheckAsync(CancellationToken cancellationToken)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        if (_client.CurrentVersion is not { } current)
        {
            SetIdle();
            return UpdateCheckOutcome.NotInstalled;
        }

        var channels = UpdatePolicy.ChannelsToCheck(settings);
        var candidates = new List<UpdateCandidate>(channels.Count);
        var failures = 0;
        foreach (var channel in channels)
        {
            var (answered, candidate) = await CheckChannelAsync(channel, cancellationToken);
            if (!answered)
            {
                failures++;
            }
            else if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        if (UpdatePolicy.Choose(current, candidates) is { } chosen)
        {
            StartDownload(chosen);
            return UpdateCheckOutcome.UpdateFound;
        }

        SetIdle();
        if (failures == channels.Count)
        {
            LogCheckOffline();
            return UpdateCheckOutcome.Offline;
        }

        var version = current.ToNormalizedString();
        LogUpToDate(version);
        return UpdateCheckOutcome.UpToDate;
    }

    /// <summary>Checks one channel within the timeout; <c>Answered</c> is false when the channel could not be checked.</summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any failure of one channel (network, feed, installer) only means that channel could not be checked.")]
    private async Task<(bool Answered, UpdateCandidate? Candidate)> CheckChannelAsync(UpdateChannel channel, CancellationToken cancellationToken)
    {
        var channelName = VelopackUpdateClient.ChannelName(channel);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.CheckTimeout);
        try
        {
            var candidate = await _client.CheckAsync(channel, timeout.Token);
            return (true, candidate);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogChannelTimedOut(channelName);
            return (false, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogChannelFailed(channelName, ex);
            return (false, null);
        }
    }

    private void StartDownload(UpdateCandidate update)
    {
        lock (_gate)
        {
            _update = update;
            _state = UpdateState.Downloading;
        }

        var version = update.Version.ToNormalizedString();
        var channel = VelopackUpdateClient.ChannelName(update.Channel);
        LogDownloading(version, channel);
        OnStateChanged();
        DownloadCompletion = DownloadAsync(update);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failed download is logged and dropped; the next check tries again.")]
    private async Task DownloadAsync(UpdateCandidate update)
    {
        var version = update.Version.ToNormalizedString();
        try
        {
            await _client.DownloadAsync(update, CancellationToken.None);
            lock (_gate)
            {
                _state = UpdateState.ReadyToInstall;
            }

            LogReadyToInstall(version);
        }
        catch (Exception ex)
        {
            LogDownloadFailed(version, ex);
            lock (_gate)
            {
                _update = null;
                _state = UpdateState.Idle;
            }
        }

        OnStateChanged();
    }

    private void SetIdle()
    {
        lock (_gate)
        {
            if (_state != UpdateState.Checking)
            {
                return;
            }

            _state = UpdateState.Idle;
        }

        OnStateChanged();
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Whether the app is installed could not be read; updates are off: {ExceptionType}")]
    private partial void LogInstallStateUnknown(string exceptionType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Automatic update check is turned off")]
    private partial void LogAutomaticCheckOff();

    [LoggerMessage(Level = LogLevel.Information, Message = "Update {Version} was downloaded earlier and will be installed on exit")]
    private partial void LogPendingUpdateFound(string version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "An update downloaded earlier could not be read")]
    private partial void LogPendingUpdateUnreadable(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The settings file could not be read; checking with the default settings: {ExceptionType}")]
    private partial void LogSettingsUnreadable(string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Checking the {Channel} update channel timed out")]
    private partial void LogChannelTimedOut(string channel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Checking the {Channel} update channel failed")]
    private partial void LogChannelFailed(string channel, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The update check failed")]
    private partial void LogCheckFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "No update channel could be checked")]
    private partial void LogCheckOffline();

    [LoggerMessage(Level = LogLevel.Information, Message = "No update found; version {Version} is current")]
    private partial void LogUpToDate(string version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloading update {Version} from the {Channel} channel")]
    private partial void LogDownloading(string version, string channel);

    [LoggerMessage(Level = LogLevel.Information, Message = "Update {Version} is downloaded and will be installed on exit")]
    private partial void LogReadyToInstall(string version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Downloading update {Version} failed")]
    private partial void LogDownloadFailed(string version, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing update {Version} on exit; restart {Restart}")]
    private partial void LogApplyingOnExit(string version, bool restart);
}
