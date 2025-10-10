using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NexGrades.App.Core;
using NexGrades.App.Features.Students;
using Wpf.Ui;

namespace NexGrades.App.Features.Subjects;

public partial class SubjectsOverviewViewModel(INavigationService navigation) : ViewModel
{
    [RelayCommand]
    public void AddSubject()
    {
        navigation.NavigateWithHierarchy(typeof(SubjectDetailPage));
    }
}