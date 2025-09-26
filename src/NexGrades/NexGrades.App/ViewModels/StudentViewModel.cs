using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NexGrades.Domain.Models;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.ViewModels;

public partial class StudentViewModel(INavigationService navigation, ISnackbarService snackbar) : ViewModel
{
    [ObservableProperty] private Student _student;
    [ObservableProperty] private string _title = "AddStudent";

    [RelayCommand]
    private void SaveStudent()
    {
        snackbar.Show("Success", "Student has been saved", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
        navigation.GoBack();
    }

    [RelayCommand]
    private void Cancel()
    {
        navigation.GoBack();
    }
}