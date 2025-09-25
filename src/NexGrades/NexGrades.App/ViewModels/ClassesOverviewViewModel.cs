using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NexGrades.Common;
using NexGrades.Domain.Models;

namespace NexGrades.App.ViewModels;

public partial class ClassesOverviewViewModel : ViewModel
{
    [ObservableProperty] 
    private ObservableCollection<Student> _students = GetFakeStudents().ToObservableCollection();

    public ClassesOverviewViewModel()
    {
        //Students = GetFakeStudents().ToObservableCollection();
    }

    private static List<Student> GetFakeStudents() =>
    [
        new()
        {
            Name = "Max Musterjunge",
            Class = new Class
            {
                Name = "1B"
            }
        },
        new()
        {
            Name = "Sina Muster"
        }
    ];
}