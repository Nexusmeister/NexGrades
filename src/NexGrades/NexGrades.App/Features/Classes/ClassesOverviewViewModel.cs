using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.App.Localization;
using NexGrades.Data;
using NexGrades.Data.Entities;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Classes;

/// <summary>
/// Lists the classes of the active school year (only active ones unless <see cref="ShowInactiveClasses"/> is set) and opens the add and edit forms for them.
/// </summary>
/// <param name="navigator">Opens the class form and hands it the id of the class to edit.</param>
/// <param name="dbContextFactory">Creates the short-lived context used to load the classes.</param>
/// <param name="snackbarService">Reports when no class is selected for editing.</param>
public partial class ClassesOverviewViewModel(
    PageNavigator navigator,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ISnackbarService snackbarService) : ViewModel
{
    [ObservableProperty]
    private ObservableCollection<ClassRow> _classes = [];

    [ObservableProperty]
    private ClassRow? _selectedClass;

    /// <summary>
    /// Whether rolled-forward and archived classes of the active year are listed too. Off by default and not
    /// persisted; the page has no editable input, so changing it reloads without an unsaved-input prompt.
    /// </summary>
    [ObservableProperty]
    private bool _showInactiveClasses;

    partial void OnShowInactiveClassesChanged(bool value)
    {
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
        await using var dbcontext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var showInactive = ShowInactiveClasses;
        var selectedId = SelectedClass?.Id;

        var classes = await dbcontext.ClassInstances
            .Where(ci => ci.SchoolYear.Status == SchoolYearStatus.Active
                && (showInactive || ci.Status == ClassInstanceStatus.Active))
            .OrderBy(ci => ci.Name)
            .Select(ci => new ClassRow(
                ci.Id,
                ci.Name,
                ci.ClassGroup.CohortLabel,
                ci.Enrollments.Count(e => e.LeftOn == null),
                ci.Status))
            .ToListAsync(cancellationToken);

        Classes = classes.ToObservableCollection();
        SelectedClass = classes.FirstOrDefault(c => c.Id == selectedId);
    }
}
