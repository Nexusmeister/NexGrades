using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.Common;
using NexGrades.Data;
using NexGrades.Data.Entities;
using Wpf.Ui;

namespace NexGrades.App.Features.Classes;

public sealed record ClassRow(int Id, string Name, string CohortLabel, int StudentCount);

public partial class ClassesOverviewViewModel(INavigationService navigation, IDbContextFactory<AppDbContext> dbContextFactory) : ViewModel
{
    [ObservableProperty]
    private ObservableCollection<ClassRow> _classes = [];

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
