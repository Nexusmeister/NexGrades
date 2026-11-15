using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private void OnSubjectsGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid
            || e.OriginalSource is not DependencyObject source
            || ItemsControl.ContainerFromElement(grid, source) is not DataGridRow row)
        {
            return;
        }

        if (ViewModel.EditSubjectCommand.CanExecute(row.Item))
        {
            ViewModel.EditSubjectCommand.Execute(row.Item);
        }
    }
}
