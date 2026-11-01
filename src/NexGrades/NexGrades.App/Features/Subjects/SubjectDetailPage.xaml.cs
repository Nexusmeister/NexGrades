using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Features.Subjects;

/// <summary>
/// Interaction logic for SubjectDetailPage.xaml
/// </summary>
public partial class SubjectDetailPage : INavigableView<SubjectDetailViewModel>
{
    /// <summary>Creates the page, which the navigation service resolves from DI together with its view model.</summary>
    /// <param name="vm">The view model that drives this page.</param>
    public SubjectDetailPage(SubjectDetailViewModel vm)
    {
        ViewModel = vm;
        DataContext = this;

        InitializeComponent();
    }

    /// <summary>The view model that drives this page.</summary>
    public SubjectDetailViewModel ViewModel { get; }
}
