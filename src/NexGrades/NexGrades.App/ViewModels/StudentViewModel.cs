using CommunityToolkit.Mvvm.ComponentModel;
using NexGrades.Domain.Models;

namespace NexGrades.App.ViewModels;

public partial class StudentViewModel : ViewModel
{
    [ObservableProperty] private Student _student;
}