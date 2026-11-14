using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using NexGrades.Domain.Services;
using NexGrades.Domain.Time;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Core.YearLock;

/// <summary>
/// The one place that knows which closed school years are unlocked right now. Pages that show or depend on the
/// lock state register here and are told about every change, instead of each polling the database. Unlocking and
/// re-locking go through it, and a single timer, armed for the earliest known expiry, announces when an unlock
/// ends so open pages turn read-only without a refresh.
/// </summary>
/// <remarks>
/// All members must be called on the UI thread, which is where the view models run. The state is not
/// synchronised, and subscribers are called directly on the calling thread, so they can touch bindable state.
/// Reusable by any page: create a <see cref="YearLockViewModel"/> with <see cref="YearLockViewModelFactory"/>
/// instead of talking to the monitor directly.
/// </remarks>
public sealed partial class SchoolYearLockMonitor
{
    /// <summary>The longest the timer sleeps; a long unlock is re-checked hourly so clock changes are noticed.</summary>
    private static readonly TimeSpan _maxCheckDelay = TimeSpan.FromHours(1);

    /// <summary>Slack added after an expiry so the check runs when the unlock has certainly ended.</summary>
    private static readonly TimeSpan _expirySlack = TimeSpan.FromSeconds(1);

    private readonly UnlockService _unlockService;
    private readonly IClock _clock;
    private readonly ISnackbarService _snackbar;
    private readonly ILogger<SchoolYearLockMonitor> _logger;
    private readonly Func<TimeSpan, Action, IDisposable> _scheduler;

    // A private messenger keeps lock changes out of the app-wide default one, and holds subscribers weakly so a
    // page that is gone needs no explicit unregistration.
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly Dictionary<int, SchoolYearLockState> _states = [];

    private IDisposable? _timer;

    /// <summary>Creates the monitor; the expiry timer is a <see cref="DispatcherTimer"/> created on first use.</summary>
    /// <param name="unlockService">Reads, sets and clears the lock state in the database.</param>
    /// <param name="clock">Source of "now" for deciding whether an unlock has ended.</param>
    /// <param name="snackbar">Tells the user when an unlock has ended.</param>
    /// <param name="logger">Logs a failed refresh; never with school year data.</param>
    public SchoolYearLockMonitor(
        UnlockService unlockService, IClock clock, ISnackbarService snackbar, ILogger<SchoolYearLockMonitor> logger)
        : this(unlockService, clock, snackbar, logger, ScheduleOnDispatcher)
    {
    }

    /// <summary>Creates the monitor with its own scheduler for the expiry timer, for tests.</summary>
    /// <param name="unlockService">Reads, sets and clears the lock state in the database.</param>
    /// <param name="clock">Source of "now" for deciding whether an unlock has ended.</param>
    /// <param name="snackbar">Tells the user when an unlock has ended.</param>
    /// <param name="logger">Logs a failed refresh.</param>
    /// <param name="scheduler">Runs the given action once after the given delay; disposing the result cancels it.</param>
    internal SchoolYearLockMonitor(
        UnlockService unlockService,
        IClock clock,
        ISnackbarService snackbar,
        ILogger<SchoolYearLockMonitor> logger,
        Func<TimeSpan, Action, IDisposable> scheduler)
    {
        _unlockService = unlockService;
        _clock = clock;
        _snackbar = snackbar;
        _logger = logger;
        _scheduler = scheduler;
    }

    /// <summary>
    /// Registers <paramref name="recipient"/> to be told about every lock state the monitor learns, of any year.
    /// The recipient is held weakly.
    /// </summary>
    /// <typeparam name="TRecipient">The subscriber type.</typeparam>
    /// <param name="recipient">The subscriber.</param>
    /// <param name="handler">
    /// Called on the UI thread with the recipient and the new state. It must not capture the recipient (use a
    /// <see langword="static"/> lambda), or the weak reference is defeated.
    /// </param>
    public void Register<TRecipient>(TRecipient recipient, Action<TRecipient, SchoolYearLockState> handler)
        where TRecipient : class =>
        _messenger.Register<TRecipient, SchoolYearLockChanged>(recipient, (r, message) => handler(r, message.State));

    /// <summary>Reads the lock state of a school year from the database and tells the subscribers.</summary>
    /// <param name="schoolYearId">The school year to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task RefreshAsync(int schoolYearId, CancellationToken cancellationToken = default)
    {
        var state = await _unlockService.GetLockStateAsync(schoolYearId, cancellationToken);
        Apply(state);
    }

    /// <summary>Unlocks a closed school year until <paramref name="until"/> and tells the subscribers.</summary>
    /// <param name="schoolYearId">The closed school year to unlock.</param>
    /// <param name="until">When the unlock ends.</param>
    /// <param name="reason">Why the year is unlocked; written to every audit entry.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task UnlockAsync(int schoolYearId, DateTime until, string reason, CancellationToken cancellationToken = default)
    {
        await _unlockService.UnlockAsync(schoolYearId, until, reason, cancellationToken);
        await RefreshAsync(schoolYearId, cancellationToken);
    }

    /// <summary>Locks a school year again immediately and tells the subscribers.</summary>
    /// <param name="schoolYearId">The school year to lock.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task RelockAsync(int schoolYearId, CancellationToken cancellationToken = default)
    {
        await _unlockService.RelockAsync(schoolYearId, cancellationToken);
        await RefreshAsync(schoolYearId, cancellationToken);
    }

    /// <summary>
    /// How long to wait before the next expiry check: until the earliest unlock that ends after
    /// <paramref name="now"/>, plus a second of slack, but at most an hour. <see langword="null"/> when no
    /// unlock is pending and no timer is needed.
    /// </summary>
    internal static TimeSpan? NextCheckDelay(IEnumerable<SchoolYearLockState> states, DateTime now)
    {
        DateTime? earliest = null;
        foreach (var state in states)
        {
            if (state.IsClosed && state.UnlockedUntil is { } until && until > now && (earliest is null || until < earliest))
            {
                earliest = until;
            }
        }

        if (earliest is not { } next)
        {
            return null;
        }

        var delay = next - now + _expirySlack;
        return delay > _maxCheckDelay ? _maxCheckDelay : delay;
    }

    /// <summary>
    /// Reads every closed school year, newest first, caches and broadcasts their states and arms the expiry timer.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The normalized states; an unlock that has already ended is reported as locked.</returns>
    public async Task<IReadOnlyList<SchoolYearLockState>> LoadClosedYearsAsync(CancellationToken cancellationToken = default)
    {
        var states = await _unlockService.GetClosedYearsAsync(cancellationToken);

        var result = new List<SchoolYearLockState>(states.Count);
        foreach (var state in states)
        {
            result.Add(Cache(state));
        }

        ArmTimer();
        return result;
    }

    /// <summary>
    /// Strips the unlock fields from a state whose unlock has ended by <paramref name="now"/>. Without this a
    /// stale unlock would be shown as active, and the timer would announce an unlock that was over before the
    /// year was loaded.
    /// </summary>
    private static SchoolYearLockState Normalize(SchoolYearLockState state, DateTime now) =>
        state.IsWritableAt(now) ? state : state with { UnlockedUntil = null, UnlockReason = null };

    /// <summary>Caches the normalized state and tells the subscribers; returns the normalized state.</summary>
    private SchoolYearLockState Cache(SchoolYearLockState state)
    {
        var normalized = Normalize(state, _clock.Now);
        _states[normalized.SchoolYearId] = normalized;
        Broadcast(normalized);
        return normalized;
    }

    private void Apply(SchoolYearLockState state)
    {
        Cache(state);
        ArmTimer();
    }

    private void ArmTimer()
    {
        _timer?.Dispose();
        _timer = null;

        if (NextCheckDelay(_states.Values, _clock.Now) is { } delay)
        {
            _timer = _scheduler(delay, OnTimerElapsed);
        }
    }

    private void OnTimerElapsed() => _ = CheckExpiredAsync();

    private async Task CheckExpiredAsync()
    {
        try
        {
            var now = _clock.Now;
            var expired = _states.Values
                .Where(state => state.IsClosed && state.UnlockedUntil is { } until && until <= now)
                .ToList();

            var announce = false;
            foreach (var cached in expired)
            {
                var current = cached;
                var read = false;
                try
                {
                    // The database is read only, so a re-lock or extension done elsewhere is noticed; nothing is
                    // written on expiry.
                    current = await _unlockService.GetLockStateAsync(cached.SchoolYearId);
                    read = true;
                }
                catch (Exception ex)
                {
                    LogRefreshFailed(ex);
                }

                // Something else (an unlock, a refresh) replaced the entry while this read was in flight; its
                // state is newer, so this stale result is dropped.
                if (!_states.TryGetValue(cached.SchoolYearId, out var present) || !ReferenceEquals(present, cached))
                {
                    continue;
                }

                if (read && current.IsClosed && !current.IsWritableAt(_clock.Now))
                {
                    announce = true;
                }

                Cache(current);
            }

            if (announce)
            {
                _snackbar.Show(
                    "Unlock ended",
                    "The unlock of a closed school year has ended. It is read-only again.",
                    ControlAppearance.Caution,
                    new SymbolIcon(SymbolRegular.LockClosed24),
                    TimeSpan.FromSeconds(8));
            }
        }
        catch (Exception ex)
        {
            LogRefreshFailed(ex);
        }

        // Also re-arms for a year whose unlock turned out to be still running.
        ArmTimer();
    }

    // The monitor is UI-thread only (see the class remarks), so the message is sent directly.
    private void Broadcast(SchoolYearLockState state) => _messenger.Send(new SchoolYearLockChanged(state));

    private static DispatcherTimerHandle ScheduleOnDispatcher(TimeSpan delay, Action callback) => new(delay, callback);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refreshing the lock state of a school year failed after an unlock expired.")]
    private partial void LogRefreshFailed(Exception exception);

    /// <summary>A one-shot <see cref="DispatcherTimer"/>; it is created only when a delay is scheduled.</summary>
    private sealed class DispatcherTimerHandle : IDisposable
    {
        private readonly DispatcherTimer _timer;

        public DispatcherTimerHandle(TimeSpan delay, Action callback)
        {
            _timer = new DispatcherTimer { Interval = delay };
            _timer.Tick += (_, _) =>
            {
                _timer.Stop();
                callback();
            };
            _timer.Start();
        }

        public void Dispose() => _timer.Stop();
    }
}
