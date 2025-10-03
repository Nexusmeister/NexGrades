using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Pages;
using NexGrades.Data;
using NexGrades.Domain.Models;
using System.Collections.ObjectModel;
using NexGrades.Common;
using Wpf.Ui;

namespace NexGrades.App.ViewModels;

public partial class StudentsOverviewViewModel(INavigationService navigation, IDbContextFactory<AppDbContext> dbContextFactory) : ViewModel
{
    [ObservableProperty]
    private ObservableCollection<Student> _students;

    [RelayCommand]
    private void OnAddStudent()
    {
        navigation.NavigateWithHierarchy(typeof(StudentPage));
    }

    [RelayCommand]
    private async Task LoadStudentsAsync(CancellationToken cancellationToken = default)
    {
        var dbcontext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var students = await (from student in dbcontext.Students.AsQueryable()
            select new Student
            {
                Id = student.Id,
                FirstName = student.FirstName,
                Name = student.LastName
            }).ToListAsync(cancellationToken);

        Students = students.ToObservableCollection();
    }
}