using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Models;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.ViewModels;

public partial class StudentViewModel(INavigationService navigation, ISnackbarService snackbar, IDbContextFactory<AppDbContext> dbContext) : ViewModel
{
    [ObservableProperty] private Student _student = new();
    [ObservableProperty] private string _title = "AddStudent";

    [RelayCommand]
    private async Task SaveStudent(CancellationToken cancellationToken = default)
    {
        var db = await dbContext.CreateDbContextAsync(cancellationToken);
        var students = db.Students;
        var studentToAdd = new StudentEntity
        {
            FirstName = Student.FirstName,
            LastName = Student.Name
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
}