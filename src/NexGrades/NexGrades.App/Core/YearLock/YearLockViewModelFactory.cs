using NexGrades.Domain.Services;
using NexGrades.Domain.Time;
using Wpf.Ui;

namespace NexGrades.App.Core.YearLock;

/// <summary>
/// Creates <see cref="YearLockViewModel"/> instances. A page injects the factory instead of the monitor, dialog
/// and snackbar the view model needs, so reusing the lock UI on a new page is one constructor parameter.
/// </summary>
/// <param name="monitor">Source of lock state changes.</param>
/// <param name="dialog">Asks for the unlock reason and duration.</param>
/// <param name="snackbar">Reports outcomes.</param>
/// <param name="clock">Source of "now".</param>
public sealed class YearLockViewModelFactory(
    SchoolYearLockMonitor monitor, IUnlockYearDialogService dialog, ISnackbarService snackbar, IClock clock)
{
    /// <summary>Creates a view model that follows no year yet; call <see cref="YearLockViewModel.ShowAsync"/>.</summary>
    /// <returns>The new view model.</returns>
    public YearLockViewModel Create() => new(monitor, dialog, snackbar, clock);

    /// <summary>Creates a view model for a year whose state is already known, for example one row of a list.</summary>
    /// <param name="initialState">The year's current lock state.</param>
    /// <returns>The new view model.</returns>
    public YearLockViewModel Create(SchoolYearLockState initialState) => new(monitor, dialog, snackbar, clock, initialState);
}
