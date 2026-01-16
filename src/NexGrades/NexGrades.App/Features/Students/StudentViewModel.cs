using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using NexGrades.App.Core;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Models;
using System.Collections.ObjectModel;
using NexGrades.Common;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Students;

public partial class StudentViewModel(INavigationService navigation, ISnackbarService snackbar, IDbContextFactory<AppDbContext> dbContextFactory) : ViewModel
{
    [ObservableProperty] private Student _student = new();
    [ObservableProperty] private ObservableCollection<Class> _classes = [];
    [ObservableProperty] private string _title = "AddStudent";

    [RelayCommand]
    private async Task InitAsync(CancellationToken cancellationToken = default)
    {
        var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var classes = await db.Classes.AsNoTracking().ToListAsync(cancellationToken);
        Classes = classes.Select(x => new Class
        {
            Id = x.Id,
            Name = x.Name
        }).ToList().ToObservableCollection();
    }

    [RelayCommand]
    private async Task SaveStudent(CancellationToken cancellationToken = default)
    {
        var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var students = db.Students;
        var studentToAdd = new StudentEntity
        {
            FirstName = Student.FirstName,
            LastName = Student.Name,
            ClassId = Student.Class.Id
        };

        students.Add(studentToAdd);
        var saved = await db.SaveChangesAsync(cancellationToken);

        if (saved != 0)
        {
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

    [RelayCommand]
    private async Task LoadClasses(CancellationToken cancellationToken = default)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var classes = await (from @class in dbContext.Classes
            orderby @class.Name
            select new Class
            {
                Id = @class.Id,
                Name = @class.Name
            }).ToListAsync(cancellationToken);

        Classes = classes.ToObservableCollection();
    }
}