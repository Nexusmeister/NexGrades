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

namespace NexGrades.App.Features.Students;

/// <summary>
/// Lists all students with their class in the active school year, and opens the add and edit forms.
/// </summary>
/// <param name="navigation">Opens the student form.</param>
/// <param name="dbContextFactory">Creates the short-lived context used to load the students.</param>
/// <param name="snackbarService">Reports when no student is selected for editing.</param>
/// <param name="editRequest">Hands the id of the selected student to the student form.</param>
public partial class StudentsOverviewViewModel(
    INavigationService navigation,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ISnackbarService snackbarService,
    StudentEditRequest editRequest) : ViewModel
{
    [ObservableProperty]
    private ObservableCollection<StudentRow> _students = [];

    [ObservableProperty]
    private StudentRow? _selectedStudent;

    [RelayCommand]
    private void OnEditStudent()
    {
        if (SelectedStudent is null)
        {
            snackbarService.Show("No student selected", "Select a student to edit.", ControlAppearance.Info, new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(5));
            return;
        }

        editRequest.StudentId = SelectedStudent.Id;
        navigation.NavigateWithHierarchy(typeof(StudentPage));
    }

    [RelayCommand]
    private void OnAddStudent()
    {
        navigation.NavigateWithHierarchy(typeof(StudentPage));
    }

    [RelayCommand]
    private async Task LoadStudentsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbcontext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // A student's "current" class is their enrollment in the currently Active school year; a student
        // with no such enrollment (e.g. imported but not yet placed) shows no class rather than being hidden.
        var students = await (from student in dbcontext.Students.AsQueryable()
                              let enrollment = student.Enrollments
                                  .Where(e => e.ClassInstance.SchoolYear.Status == SchoolYearStatus.Active && e.LeftOn == null)
                                  .OrderByDescending(e => e.JoinedOn)
                                  .FirstOrDefault()
                              orderby student.LastName, student.FirstName
                              select new StudentRow(
                                  student.Id,
                                  student.FirstName,
                                  student.LastName,
                                  enrollment != null ? enrollment.ClassInstance.Name : string.Empty))
            .ToListAsync(cancellationToken);

        Students = students.ToObservableCollection();
    }
}
