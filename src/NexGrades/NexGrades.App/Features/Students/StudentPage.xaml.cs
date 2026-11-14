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

        // Subscribe only while the page is shown, so a page that is no longer displayed is not kept alive.
        Loaded += (_, _) =>
        {
            ViewModel.Grades.PropertyChanged -= OnGradesPropertyChanged;
            ViewModel.Grades.PropertyChanged += OnGradesPropertyChanged;
        };
        Unloaded += (_, _) => ViewModel.Grades.PropertyChanged -= OnGradesPropertyChanged;
    }

    /// <summary>The view model that drives this page.</summary>
    public StudentViewModel ViewModel { get; }

    /// <summary>
    /// The grade editor is the first item of the scrolling tab, so opening it for a bucket further down would
    /// happen out of sight. Scrolling to the top brings it into view.
    /// </summary>
    private void OnGradesPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StudentGradesViewModel.ShowEditor)
            or nameof(StudentGradesViewModel.EditorBucketName)
            or nameof(StudentGradesViewModel.EditorTitle)
            && ViewModel.Grades.ShowEditor)
        {
            GradesScroller.ScrollToTop();
        }
    }
}
