using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NexGrades.App.Pages;
using NexGrades.Common;
using NexGrades.Domain.Models;
using Wpf.Ui;

namespace NexGrades.App.ViewModels;

public partial class ClassesOverviewViewModel(INavigationService navigation) : ViewModel
{
    [ObservableProperty] 
    private ObservableCollection<Student> _students = GetFakeStudents().ToObservableCollection();
    
    [RelayCommand]
    private void OnAddStudent()
    {
        navigation.NavigateWithHierarchy(typeof(StudentPage));
    }

    private static List<Student> GetFakeStudents() =>
    [
        new()
        {
            Name = "Max Musterjunge",
            FirstName = "Max",
            Class = new Class
            {
                Name = "1B"
            }
        },
        new()
        {
            FirstName = "Sina",
            Name = "Sina Muster"
        }
    ];
}