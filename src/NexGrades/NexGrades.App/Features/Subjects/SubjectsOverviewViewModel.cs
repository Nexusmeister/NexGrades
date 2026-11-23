using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.Data;

namespace NexGrades.App.Features.Subjects;

/// <summary>Lists the subject catalogue and opens the form to add or edit a subject.</summary>
/// <param name="navigator">Opens the subject form.</param>
/// <param name="dbContextFactory">Creates the short-lived context used to load the subjects.</param>
public partial class SubjectsOverviewViewModel(PageNavigator navigator, IDbContextFactory<AppDbContext> dbContextFactory) : ViewModel
{
    [ObservableProperty]
    private ObservableCollection<SubjectRow> _subjects = [];

    /// <summary>Opens the form for a new subject.</summary>
    [RelayCommand]
    public void AddSubject()
    {
        navigator.NavigateWithHierarchy<SubjectDetailPage>();
    }

    /// <summary>Opens the form to edit one subject's name and short code.</summary>
    /// <param name="subject">The subject row to edit.</param>
    [RelayCommand]
    private void EditSubject(SubjectRow subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        navigator.NavigateWithHierarchy<SubjectDetailPage>(subject.Id);
    }

    [RelayCommand]
    private async Task LoadSubjectsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbcontext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var subjects = await dbcontext.Subjects
            .OrderBy(s => s.Name)
            .Select(s => new SubjectRow(s.Id, s.Name, s.ShortCode))
            .ToListAsync(cancellationToken);

        Subjects = subjects.ToObservableCollection();
    }
}
