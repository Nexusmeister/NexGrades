using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Models;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.ViewModels;

public partial class ClassViewModel(INavigationService navigation, ISnackbarService snackbar, IDbContextFactory<AppDbContext> dbContextFactory) : ViewModel
{
    [ObservableProperty] private Class _class = new();
    [ObservableProperty] private string _title = "AddClass";

    [RelayCommand]
    private async Task SaveClass(CancellationToken cancellationToken = default)
    {
        var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var classes = db.Classes;
        var classToAdd = new ClassEntity()
        {
            Name = Class.Name
        };

        classes.Add(classToAdd);
        var saved = await db.SaveChangesAsync(cancellationToken);

        if (saved != 0)
        {
            snackbar.Show("Success", "Class has been saved", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
            navigation.GoBack();
        }
        else
        {
            snackbar.Show("Error", "Class has not been saved", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        navigation.GoBack();
    }

}