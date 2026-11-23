using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Localization;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;
using NexGrades.Domain.Time;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Core.YearLock;

/// <summary>
/// The reusable lock state of one school year, for any page that shows or depends on it: whether the year is
/// locked, unlocked or neither (not closed), the texts that explain it, and the commands to unlock and re-lock it.
/// It follows the year through <see cref="SchoolYearLockMonitor"/>, so it turns read-only by itself when an unlock
/// expires. Create it with <see cref="YearLockViewModelFactory"/>.
/// </summary>
/// <remarks>
/// A page that guards writes binds its write commands to <see cref="IsWritable"/> and re-evaluates them on
/// <see cref="WritabilityChanged"/>. A page with a year picker calls <see cref="ShowAsync"/> whenever the selected
/// year changes; a list of years creates one view model per year, with an initial state. Create it on the UI thread.
/// </remarks>
public sealed partial class YearLockViewModel : ObservableObject
{
    private readonly SchoolYearLockMonitor _monitor;
    private readonly IUnlockYearDialogService _dialog;
    private readonly ISnackbarService _snackbar;
    private readonly IClock _clock;
    private readonly ILogger<YearLockViewModel> _logger;

    private int? _schoolYearId;
    private SchoolYearLockState? _state;
    private bool _isWritable;

    /// <summary>Creates the view model and starts following the lock state of the year it is given.</summary>
    /// <param name="monitor">Source of lock state changes, and the way to unlock and re-lock.</param>
    /// <param name="dialog">Asks for the reason and the duration when unlocking.</param>
    /// <param name="snackbar">Reports the outcome of unlocking and re-locking.</param>
    /// <param name="clock">Source of "now" for deciding whether the year is writable.</param>
    /// <param name="initialState">The year's state when it is already known; <see langword="null"/> for none yet.</param>
    /// <param name="logger">Logs an unexpected failure without any pupil data; <see langword="null"/> logs nothing.</param>
    internal YearLockViewModel(
        SchoolYearLockMonitor monitor,
        IUnlockYearDialogService dialog,
        ISnackbarService snackbar,
        IClock clock,
        SchoolYearLockState? initialState = null,
        ILogger<YearLockViewModel>? logger = null)
    {
        _monitor = monitor;
        _dialog = dialog;
        _snackbar = snackbar;
        _clock = clock;
        _logger = logger ?? NullLogger<YearLockViewModel>.Instance;

        _schoolYearId = initialState?.SchoolYearId;
        _state = initialState;
        _isWritable = initialState is not null && initialState.IsWritableAt(clock.Now);

        // Static, so the monitor's weak reference to this view model is not defeated.
        monitor.Register(this, static (viewModel, state) => viewModel.OnStateChanged(state));
    }

    /// <summary>Raised when <see cref="IsWritable"/> changes, so a page can re-evaluate its write commands.</summary>
    public event EventHandler? WritabilityChanged;

    /// <summary>The school year being followed, or <see langword="null"/> when none is shown.</summary>
    public int? SchoolYearId => _schoolYearId;

    /// <summary>The last known lock state; <see langword="null"/> until it has been read.</summary>
    public SchoolYearLockState? State => _state;

    /// <summary>The year's display label, e.g. "2025/26"; empty until the state is known.</summary>
    public string Label => State?.Label ?? string.Empty;

    /// <summary>True when the year is closed, whether or not it is unlocked.</summary>
    public bool IsClosed => State?.IsClosed ?? false;

    /// <summary>
    /// True when writes to the year are allowed right now: the year is not closed, or it is unlocked. False while
    /// the state is unknown, so a page stays read-only until it knows. Evaluated each time the state changes.
    /// </summary>
    public bool IsWritable => _isWritable;

    /// <summary>True when the year is closed and read-only; the state in which it can be unlocked.</summary>
    public bool IsLocked => IsClosed && !IsWritable;

    /// <summary>True when the year is closed but temporarily writable; the state in which it can be locked again.</summary>
    public bool IsUnlocked => IsClosed && IsWritable;

    /// <summary>When the year was closed, formatted as a short date; empty when unknown.</summary>
    public string ClosedOnText => State?.ClosedOn is { } closedOn ? closedOn.ToString("d", CultureInfo.CurrentCulture) : string.Empty;

    /// <summary>Tells the user when the year was closed, as one translatable sentence; empty when unknown.</summary>
    public string ClosedOnMessage =>
        State?.ClosedOn is null ? string.Empty : TextResources.Format("Core_YearLock_ClosedOn_Format", ClosedOnText);

    /// <summary>When the unlock ends, formatted for the current culture; empty when the year is not unlocked.</summary>
    public string UnlockedUntilText =>
        IsUnlocked && State?.UnlockedUntil is { } until ? until.ToString("g", CultureInfo.CurrentCulture) : string.Empty;

    /// <summary>Why the year was unlocked; empty when it is not unlocked.</summary>
    public string UnlockReason => IsUnlocked ? State?.UnlockReason ?? string.Empty : string.Empty;

    /// <summary>Explains a locked year to the user.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Bound from XAML, which needs an instance property.")]
    public string LockedMessage => TextResources.Text("Core_YearLock_Locked_Message");

    /// <summary>Explains an unlocked year to the user, with the time the unlock ends.</summary>
    public string UnlockedMessage => TextResources.Format("Core_YearLock_Unlocked_Message_Format", UnlockedUntilText);

    /// <summary>
    /// Follows another school year: clears the shown state at once, so the previous year's is never shown for the
    /// new one, then reads the new state. A failure is reported in a snackbar.
    /// </summary>
    /// <param name="schoolYearId">The year to follow, or <see langword="null"/> to show none.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task ShowAsync(int? schoolYearId, CancellationToken cancellationToken = default)
    {
        if (schoolYearId != _schoolYearId)
        {
            _schoolYearId = schoolYearId;
            SetState(null);
        }

        await RefreshAsync(cancellationToken);
    }

    /// <summary>
    /// Reads the lock state of the followed year again, for example after a write was refused because the year
    /// had been locked meanwhile. Does nothing when no year is shown; a failure is reported in a snackbar.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_schoolYearId is not { } schoolYearId)
        {
            return;
        }

        try
        {
            await _monitor.RefreshAsync(schoolYearId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowError(TextResources.Text("Core_YearLock_Refresh_Failed"));
        }
    }

    /// <summary>Asks for a reason and a duration, then unlocks the year.</summary>
    [RelayCommand(CanExecute = nameof(IsLocked))]
    private async Task UnlockAsync(CancellationToken cancellationToken = default)
    {
        if (_schoolYearId is not { } schoolYearId)
        {
            return;
        }

        var label = Label;
        var request = await _dialog.AskAsync(label, cancellationToken);
        if (request is null)
        {
            return;
        }

        try
        {
            await _monitor.UnlockAsync(schoolYearId, request.Until, request.Reason, cancellationToken);
        }
        catch (Exception ex) when (ex is InvariantViolationException or DbUpdateException)
        {
            ShowError(TextResources.Text("Core_YearLock_Unlock_Failed"));
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The monitor reads the state again after unlocking; that read can fail although the unlock went through.
            LogLockChangeFailed(ex);
            ShowError(TextResources.Text("Core_YearLock_Unlock_StateFailed"));
            return;
        }

        ShowSuccess(TextResources.Format("Core_YearLock_Unlock_Success_Format", label, request.Until.ToString("g", CultureInfo.CurrentCulture)));
    }

    /// <summary>Locks the year again at once.</summary>
    [RelayCommand(CanExecute = nameof(IsUnlocked))]
    private async Task RelockAsync(CancellationToken cancellationToken = default)
    {
        if (_schoolYearId is not { } schoolYearId)
        {
            return;
        }

        var label = Label;
        try
        {
            await _monitor.RelockAsync(schoolYearId, cancellationToken);
        }
        catch (Exception ex) when (ex is InvariantViolationException or DbUpdateException)
        {
            ShowError(TextResources.Text("Core_YearLock_Relock_Failed"));
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The monitor reads the state again after locking; that read can fail although the lock went through.
            LogLockChangeFailed(ex);
            ShowError(TextResources.Text("Core_YearLock_Relock_StateFailed"));
            return;
        }

        ShowSuccess(TextResources.Format("Core_YearLock_Relock_Success_Format", label));
    }

    private void OnStateChanged(SchoolYearLockState state)
    {
        // The monitor tells about every year; only the followed one matters.
        if (state.SchoolYearId == _schoolYearId)
        {
            SetState(state);
        }
    }

    private void SetState(SchoolYearLockState? state)
    {
        _state = state;

        var writable = state is not null && state.IsWritableAt(_clock.Now);
        var flipped = writable != _isWritable;
        _isWritable = writable;

        OnPropertyChanged(nameof(SchoolYearId));
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(IsClosed));
        OnPropertyChanged(nameof(IsWritable));
        OnPropertyChanged(nameof(IsLocked));
        OnPropertyChanged(nameof(IsUnlocked));
        OnPropertyChanged(nameof(ClosedOnText));
        OnPropertyChanged(nameof(ClosedOnMessage));
        OnPropertyChanged(nameof(UnlockedUntilText));
        OnPropertyChanged(nameof(UnlockedMessage));
        OnPropertyChanged(nameof(UnlockReason));
        UnlockCommand.NotifyCanExecuteChanged();
        RelockCommand.NotifyCanExecuteChanged();

        if (flipped)
        {
            WritabilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ShowSuccess(string message) =>
        _snackbar.Show(TextResources.Text("Common_Success"), message, ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));

    private void ShowError(string message) =>
        _snackbar.Show(TextResources.Text("Common_Error"), message, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));

    [LoggerMessage(Level = LogLevel.Error, Message = "Changing the lock state of a school year failed unexpectedly.")]
    private partial void LogLockChangeFailed(Exception exception);
}
