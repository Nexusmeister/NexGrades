using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.Common;
using NexGrades.Data;
using NexGrades.Data.Entities;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Classes;

/// <summary>
/// Lists the classes of the active school year and opens the add and edit forms for them.
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

    [RelayCommand]
    private void OnEditClass()
    {
        if (SelectedClass is null)
        {
            snackbarService.Show("", "Please select a class to edit.", ControlAppearance.Info, new SymbolIcon(SymbolRegular.Info32), TimeSpan.FromSeconds(5));
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

        var classes = await dbcontext.ClassInstances
            .Where(ci => ci.SchoolYear.Status == SchoolYearStatus.Active)
            .OrderBy(ci => ci.Name)
            .Select(ci => new ClassRow(
                ci.Id,
                ci.Name,
                ci.ClassGroup.CohortLabel,
                ci.Enrollments.Count(e => e.LeftOn == null)))
            .ToListAsync(cancellationToken);

        Classes = classes.ToObservableCollection();
    }
}
