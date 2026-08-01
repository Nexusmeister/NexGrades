using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.Common;
using NexGrades.Data;
using Wpf.Ui;

namespace NexGrades.App.Features.Subjects;

public sealed record SubjectRow(int Id, string Name, string ShortCode);

public partial class SubjectsOverviewViewModel(INavigationService navigation, IDbContextFactory<AppDbContext> dbContextFactory) : ViewModel
{
    [ObservableProperty]
    private ObservableCollection<SubjectRow> _subjects = [];

    [RelayCommand]
    public void AddSubject()
    {
        navigation.NavigateWithHierarchy(typeof(SubjectDetailPage));
    }

    [RelayCommand]
    private async Task LoadSubjectsAsync(CancellationToken cancellationToken = default)
    {
        var dbcontext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var subjects = await dbcontext.Subjects
            .OrderBy(s => s.Name)
            .Select(s => new SubjectRow(s.Id, s.Name, s.ShortCode))
            .ToListAsync(cancellationToken);

        Subjects = subjects.ToObservableCollection();
    }
}
