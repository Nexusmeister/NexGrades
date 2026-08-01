using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.Common;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;
using System.Collections.ObjectModel;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Students;

public sealed record ClassInstanceOption(int Id, string Name);

public partial class StudentViewModel(
    INavigationService navigation,
    ISnackbarService snackbar,
    IDbContextFactory<AppDbContext> dbContextFactory,
    EnrollmentService enrollmentService) : ViewModel
{
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private ObservableCollection<ClassInstanceOption> _classes = [];
    [ObservableProperty] private ClassInstanceOption? _selectedClass;
    [ObservableProperty] private string _title = "AddStudent";

    [RelayCommand]
    private async Task InitAsync(CancellationToken cancellationToken = default)
    {
        var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Only classes of the currently Active school year can be assigned — enrolling into a Planned or
        // Closed year's class doesn't mean anything yet (no rollover/close workflow exists to have produced one).
        var classes = await db.ClassInstances
            .Where(ci => ci.SchoolYear.Status == SchoolYearStatus.Active && ci.Status == ClassInstanceStatus.Active)
            .OrderBy(ci => ci.Name)
            .Select(ci => new ClassInstanceOption(ci.Id, ci.Name))
            .ToListAsync(cancellationToken);

        Classes = classes.ToObservableCollection();
    }

    [RelayCommand]
    private async Task SaveStudent(CancellationToken cancellationToken = default)
    {
        if (SelectedClass is null)
        {
            snackbar.Show(
                "Missing class",
                "Select a class for the student before saving.",
                ControlAppearance.Caution,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(5));
            return;
        }

        var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var studentToAdd = new Student
        {
            FirstName = FirstName,
            LastName = LastName,
        };

        db.Students.Add(studentToAdd);
        var saved = await db.SaveChangesAsync(cancellationToken);

        if (saved != 0)
        {
            await enrollmentService.CreateEnrollmentAsync(studentToAdd.Id, SelectedClass.Id, DateTime.Now, cancellationToken);

            snackbar.Show("Success", "Student has been saved", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
            navigation.GoBack();
        }
        else
        {
            snackbar.Show("Error", "Student has not been saved", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        navigation.GoBack();
    }
}
