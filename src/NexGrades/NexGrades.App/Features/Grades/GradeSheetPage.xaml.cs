using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Features.Grades;

/// <summary>
/// The grade sheet page of one subject instance.
/// </summary>
public partial class GradeSheetPage : INavigableView<GradeSheetViewModel>
{
    /// <summary>Creates the page, which the navigation service resolves from DI together with its view model.</summary>
    /// <param name="vm">The view model that drives this page.</param>
    public GradeSheetPage(GradeSheetViewModel vm)
    {
        ViewModel = vm;
        DataContext = this;

        InitializeComponent();
    }

    /// <summary>The view model that drives this page.</summary>
    public GradeSheetViewModel ViewModel { get; }
}
