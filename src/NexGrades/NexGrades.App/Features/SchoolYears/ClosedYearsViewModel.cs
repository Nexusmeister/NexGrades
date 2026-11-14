using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NexGrades.App.Core.YearLock;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.SchoolYears;

/// <summary>
/// The "Closed school years" list: one <see cref="YearLockViewModel"/> per closed year, each with its unlock and
/// lock-again commands.
/// </summary>
/// <param name="monitor">Reads the closed years and announces their lock changes.</param>
/// <param name="factory">Creates the per-year lock view model.</param>
/// <param name="snackbar">Reports a failed load.</param>
public sealed partial class ClosedYearsViewModel(
    SchoolYearLockMonitor monitor, YearLockViewModelFactory factory, ISnackbarService snackbar) : ObservableObject
{
    /// <summary>The closed school years, newest first.</summary>
    public ObservableCollection<YearLockViewModel> Years { get; } = [];

    /// <summary>True when there is at least one closed year; the section is hidden otherwise.</summary>
    public bool HasClosedYears => Years.Count > 0;

    /// <summary>Reads the closed years again and replaces the list.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var states = await monitor.LoadClosedYearsAsync(cancellationToken);

            Years.Clear();
            foreach (var state in states)
            {
                Years.Add(factory.Create(state));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            snackbar.Show(
                "Error",
                "The closed school years could not be loaded.",
                ControlAppearance.Caution,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(5));
        }

        OnPropertyChanged(nameof(HasClosedYears));
    }
}
