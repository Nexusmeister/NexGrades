using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.Common;
using NexGrades.Data;
using NexGrades.Domain.Models;
using System.Collections.ObjectModel;
using Wpf.Ui;

namespace NexGrades.App.ViewModels;

public partial class ClassesOverviewViewModel(INavigationService navigation, IDbContextFactory<AppDbContext> dbContextFactory) : ViewModel
{
    [ObservableProperty] 
    private ObservableCollection<Class> _classes = [];
    
    [RelayCommand]
    private void OnAddClass()
    {
        //navigation.NavigateWithHierarchy(typeof(StudentPage));
    }

    [RelayCommand]
    private async Task LoadClassesAsync(CancellationToken cancellationToken = default)
    {
        var dbcontext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var classes = await (from schoolClass in dbcontext.Classes.AsQueryable()
            select new Class
            {
                Id = schoolClass.Id,
                Name = schoolClass.Name
            }).ToListAsync(cancellationToken);

        Classes = classes.ToObservableCollection();
    }
}