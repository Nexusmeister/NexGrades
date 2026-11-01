using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Features.Students;

/// <summary>
/// Interaction logic for StudentsOverviewPage.xaml
/// </summary>
public partial class StudentsOverviewPage : INavigableView<StudentsOverviewViewModel>
{
    /// <summary>Creates the page, which the navigation service resolves from DI together with its view model.</summary>
    /// <param name="vm">The view model that drives this page.</param>
    public StudentsOverviewPage(StudentsOverviewViewModel vm)
    {
        ViewModel = vm;
        DataContext = this;

        InitializeComponent();
    }

    /// <summary>The view model that drives this page.</summary>
    public StudentsOverviewViewModel ViewModel { get; }

    /// <summary>
    /// Opens the double-clicked student. Only a click on a row counts: a double-click on a column header or the
    /// empty area below the rows would otherwise open whichever student happened to be selected.
    /// </summary>
    private void OnStudentsGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid
            || e.OriginalSource is not DependencyObject source
            || ItemsControl.ContainerFromElement(grid, source) is not DataGridRow)
        {
            return;
        }

        if (ViewModel.EditStudentCommand.CanExecute(null))
        {
            ViewModel.EditStudentCommand.Execute(null);
        }
    }
}
