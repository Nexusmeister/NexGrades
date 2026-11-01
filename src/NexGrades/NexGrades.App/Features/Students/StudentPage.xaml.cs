using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Features.Students;

/// <summary>
/// Interaction logic for StudentPage.xaml
/// </summary>
public partial class StudentPage : INavigableView<StudentViewModel>
{
    /// <summary>Creates the page, which the navigation service resolves from DI together with its view model.</summary>
    /// <param name="vm">The view model that drives this page.</param>
    public StudentPage(StudentViewModel vm)
    {
        ViewModel = vm;
        DataContext = this;

        InitializeComponent();
    }

    /// <summary>The view model that drives this page.</summary>
    public StudentViewModel ViewModel { get; }
}
