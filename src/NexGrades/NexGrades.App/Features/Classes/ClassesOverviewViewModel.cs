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

public sealed record ClassRow(int Id, string Name, string CohortLabel, int StudentCount);

public partial class ClassesOverviewViewModel(
    INavigationService navigation,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ISnackbarService snackbarService,
    ClassEditRequest editRequest) : ViewModel
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

        editRequest.ClassInstanceId = SelectedClass.Id;
        navigation.NavigateWithHierarchy(typeof(ClassPage));
    }

    [RelayCommand]
    private void OnAddClass()
    {
        navigation.NavigateWithHierarchy(typeof(ClassPage));
    }

    [RelayCommand]
    private async Task LoadClassesAsync(CancellationToken cancellationToken = default)
    {
        var dbcontext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

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
