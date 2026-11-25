using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexGrades.App.Core;
using NexGrades.App.Localization;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Classes;

/// <summary>
/// Lists the classes of the school year chosen in <see cref="SelectedYear"/> (the Active year until the user picks
/// another; only active classes unless <see cref="ShowInactiveClasses"/> is set) and opens the add and edit forms for them.
/// </summary>
/// <param name="navigator">Opens the class form and hands it the id of the class to edit.</param>
/// <param name="dbContextFactory">Creates the short-lived context used to load the classes.</param>
/// <param name="schoolYearService">Lists the school years offered in the year selector.</param>
/// <param name="snackbarService">Reports when no class is selected for editing.</param>
/// <param name="logger">Records a failed load, without any record content.</param>
public partial class ClassesOverviewViewModel(
    PageNavigator navigator,
    IDbContextFactory<AppDbContext> dbContextFactory,
    SchoolYearService schoolYearService,
    ISnackbarService snackbarService,
    ILogger<ClassesOverviewViewModel> logger) : ViewModel
{
    private int _loadVersion;
    private bool _isApplyingLoad;

    /// <summary>
    /// True once the first load has finished, successfully or not, so the page shows its empty-state text only
    /// after the list is known to be empty rather than while it is still loading.
    /// </summary>
    [ObservableProperty]
    private bool _hasLoaded;

    [ObservableProperty]
    private ObservableCollection<ClassRow> _classes = [];

    [ObservableProperty]
    private ClassRow? _selectedClass;

    /// <summary>All school years, newest first, offered by the year selector. Refreshed by every load.</summary>
    [ObservableProperty]
    private ObservableCollection<SchoolYearSummary> _years = [];

    /// <summary>
    /// The school year whose classes are listed. The first load picks the Active year (the newest year when none is
    /// Active); later loads keep the user's choice. Not persisted, so every app start begins at the Active year.
    /// It is <see langword="null"/> only while there are no years.
    /// </summary>
    [ObservableProperty]
    private SchoolYearSummary? _selectedYear;

    /// <summary>
    /// Whether rolled-forward and archived classes of the selected year are listed too. Off by default and not
    /// persisted; the page has no editable input, so changing it reloads without an unsaved-input prompt.
    /// </summary>
    [ObservableProperty]
    private bool _showInactiveClasses;

    partial void OnShowInactiveClassesChanged(bool value)
    {
        LoadClassesCommand.Execute(null);
    }

    partial void OnSelectedYearChanged(SchoolYearSummary? value)
    {
        // The load assigns the year itself (and the selector may clear it while the list is replaced); only a
        // choice made by the user starts a new load.
        if (_isApplyingLoad)
        {
            return;
        }

        LoadClassesCommand.Execute(null);
    }

    [RelayCommand]
    private void OnEditClass()
    {
        if (SelectedClass is null)
        {
            snackbarService.Show(TextResources.Text("Common_Info"), TextResources.Text("Classes_Overview_SelectToEdit"), ControlAppearance.Info, new SymbolIcon(SymbolRegular.Info32), TimeSpan.FromSeconds(5));
            return;
        }

        navigator.NavigateWithHierarchy<ClassPage>(SelectedClass.Id);
    }

    [RelayCommand]
    private void OnAddClass()
    {
        navigator.NavigateWithHierarchy<ClassPage>();
    }

    [RelayCommand]
    private async Task LoadClassesAsync(CancellationToken cancellationToken = default)
    {
        // The newest run wins: a slower, older run (an earlier filter toggle or year change) discards its result.
        var version = ++_loadVersion;
        var showInactive = ShowInactiveClasses;
        var selectedId = SelectedClass?.Id;
        var requestedYearId = SelectedYear?.Id;

        try
        {
            var years = await schoolYearService.GetYearsAsync(cancellationToken);

            // Keep the user's year while it exists; otherwise (first load) take the Active year, else the newest.
            var year = years.FirstOrDefault(y => y.Id == requestedYearId)
                ?? years.FirstOrDefault(y => y.Status == SchoolYearStatus.Active)
                ?? (years.Count > 0 ? years[0] : null);

            await using var dbcontext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var yearId = year?.Id;
            var classes = await dbcontext.ClassInstances
                .AsNoTracking()
                .Where(ci => ci.SchoolYearId == yearId
                    && (showInactive || ci.Status == ClassInstanceStatus.Active))
                .OrderBy(ci => ci.Name)
                .Select(ci => new ClassRow(
                    ci.Id,
                    ci.Name,
                    ci.ClassGroup.CohortLabel,
                    ci.Enrollments.Count(e => e.LeftOn == null),
                    ci.Status))
                .ToListAsync(cancellationToken);

            if (version != _loadVersion)
            {
                return;
            }

            ApplyLoad(years, year, classes, selectedId);
        }
        catch (OperationCanceledException)
        {
            // The command cancels the previous run when it is executed again; a newer run has taken over, so there
            // is nothing to report and the toolkit must not see the exception.
        }
        catch (Exception ex)
        {
            LogLoadFailed(ex);

            if (version == _loadVersion)
            {
                HasLoaded = true;
            }
        }
    }

    private void ApplyLoad(IReadOnlyList<SchoolYearSummary> years, SchoolYearSummary? year, List<ClassRow> classes, int? selectedId)
    {
        _isApplyingLoad = true;
        try
        {
            Years = years.ToObservableCollection();
            SelectedYear = year;
        }
        finally
        {
            _isApplyingLoad = false;
        }

        Classes = classes.ToObservableCollection();
        SelectedClass = classes.FirstOrDefault(c => c.Id == selectedId);
        HasLoaded = true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading the class list failed.")]
    private partial void LogLoadFailed(Exception exception);
}
