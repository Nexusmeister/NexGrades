using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Features.Classes;

/// <summary>
/// Overview of the classes in the active school year. Opens the add and edit forms.
/// </summary>
public partial class ClassesOverviewPage : INavigableView<ClassesOverviewViewModel>
{
    /// <summary>Creates the page, which the navigation service resolves from DI together with its view model.</summary>
    /// <param name="vm">The view model that drives this page.</param>
    public ClassesOverviewPage(ClassesOverviewViewModel vm)
    {
        ViewModel = vm;
        DataContext = this;

        InitializeComponent();
    }

    /// <summary>The view model that drives this page.</summary>
    public ClassesOverviewViewModel ViewModel { get; }
}
