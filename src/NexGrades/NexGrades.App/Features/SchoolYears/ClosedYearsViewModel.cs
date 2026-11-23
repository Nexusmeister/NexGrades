using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NexGrades.App.Core.YearLock;
using NexGrades.App.Localization;
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

    /// <summary>True when the last load failed, so the list may be missing or out of date.</summary>
    public bool LoadFailed { get; private set; }

    /// <summary>True when the load succeeded and found no closed year; a failed load is not "none".</summary>
    public bool ShowNoClosedYears => !LoadFailed && !HasClosedYears;

    /// <summary>Reads the closed years again and replaces the list.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var states = await monitor.LoadClosedYearsAsync(cancellationToken);
            LoadFailed = false;

            Years.Clear();
            foreach (var state in states)
            {
                Years.Add(factory.Create(state));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LoadFailed = true;
            snackbar.Show(
                TextResources.Text("Common_Error"),
                TextResources.Text("SchoolYears_Closed_LoadFailed"),
                ControlAppearance.Caution,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(5));
        }

        OnPropertyChanged(nameof(HasClosedYears));
        OnPropertyChanged(nameof(LoadFailed));
        OnPropertyChanged(nameof(ShowNoClosedYears));
    }
}
