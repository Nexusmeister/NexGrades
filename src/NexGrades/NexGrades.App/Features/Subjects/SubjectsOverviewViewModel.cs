using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.Common;
using NexGrades.Data;
using NexGrades.Domain.Models;
using System.Collections.ObjectModel;
using Wpf.Ui;

namespace NexGrades.App.Features.Subjects;

public partial class SubjectsOverviewViewModel(INavigationService navigation, IDbContextFactory<AppDbContext> dbContextFactory) : ViewModel
{
    [ObservableProperty]
    private ObservableCollection<Subject> _subjects;

    [RelayCommand]
    public void AddSubject()
    {
        navigation.NavigateWithHierarchy(typeof(SubjectDetailPage));
    }

    [RelayCommand]
    private async Task LoadSubjectsAsync(CancellationToken cancellationToken = default)
    {
        var dbcontext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var students = await (from subject in dbcontext.Subjects.AsQueryable()
            select new Subject()
            {
                Id = subject.Id,
                Name = subject.Name
            }).ToListAsync(cancellationToken);

        Subjects = students.ToObservableCollection();
    }
}