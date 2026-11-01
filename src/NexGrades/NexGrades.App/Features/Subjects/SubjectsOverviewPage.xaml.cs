using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Features.Subjects;

/// <summary>
/// Interaction logic for SubjectsOverviewPage.xaml
/// </summary>
public partial class SubjectsOverviewPage : INavigableView<SubjectsOverviewViewModel>
{
    /// <summary>Creates the page, which the navigation service resolves from DI together with its view model.</summary>
    /// <param name="vm">The view model that drives this page.</param>
    public SubjectsOverviewPage(SubjectsOverviewViewModel vm)
    {
        ViewModel = vm;
        DataContext = this;

        InitializeComponent();
    }

    /// <summary>The view model that drives this page.</summary>
    public SubjectsOverviewViewModel ViewModel { get; }
}
