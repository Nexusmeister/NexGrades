using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NexGrades.App.Core;
using NexGrades.Domain.Models;
using Wpf.Ui;

namespace NexGrades.App.Features.Subjects;

public partial class SubjectDetailViewModel(INavigationService navigation) : ViewModel
{
    [ObservableProperty] private string _title = "AddSubject";
    [ObservableProperty] private Subject _subject = new();

    [RelayCommand]
    private void Cancel()
    {
        navigation.GoBack();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {

    }
}