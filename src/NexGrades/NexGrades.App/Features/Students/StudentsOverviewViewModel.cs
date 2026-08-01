using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.Common;
using NexGrades.Data;
using NexGrades.Data.Entities;
using Wpf.Ui;

namespace NexGrades.App.Features.Students;

public sealed record StudentRow(int Id, string FirstName, string LastName, string ClassName);

public partial class StudentsOverviewViewModel(INavigationService navigation, IDbContextFactory<AppDbContext> dbContextFactory) : ViewModel
{
    [ObservableProperty]
    private ObservableCollection<StudentRow> _students = [];

    [RelayCommand]
    private void OnAddStudent()
    {
        navigation.NavigateWithHierarchy(typeof(StudentPage));
    }

    [RelayCommand]
    private async Task LoadStudentsAsync(CancellationToken cancellationToken = default)
    {
        var dbcontext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

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
