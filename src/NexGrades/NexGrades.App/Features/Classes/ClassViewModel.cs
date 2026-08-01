using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.Data;
using NexGrades.Data.Entities;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Classes;

public partial class ClassViewModel(INavigationService navigation, ISnackbarService snackbar, IDbContextFactory<AppDbContext> dbContextFactory) : ViewModel
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _title = "AddClass";

    [RelayCommand]
    private async Task SaveClass(CancellationToken cancellationToken = default)
    {
        var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var activeYear = await db.SchoolYears.FirstOrDefaultAsync(sy => sy.Status == SchoolYearStatus.Active, cancellationToken);
        if (activeYear is null)
        {
            snackbar.Show("Error", "No active school year found.", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
            return;
        }

        // A brand-new class (as opposed to one rolled forward from a prior year) starts life as its own
        // cohort: the ClassGroup and its first ClassInstance share the same name.
        var classGroup = new ClassGroup
        {
            CohortLabel = Name,
            FoundedInSchoolYearId = activeYear.Id,
        };
        db.ClassGroups.Add(classGroup);
        await db.SaveChangesAsync(cancellationToken);

        var classInstance = new ClassInstance
        {
            ClassGroupId = classGroup.Id,
            SchoolYearId = activeYear.Id,
            Name = Name,
        };
        db.ClassInstances.Add(classInstance);
        var saved = await db.SaveChangesAsync(cancellationToken);

        if (saved != 0)
        {
            snackbar.Show("Success", "Class has been saved", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
            navigation.GoBack();
        }
        else
        {
            snackbar.Show("Error", "Class has not been saved", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        navigation.GoBack();
    }
}
