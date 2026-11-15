using System.Windows.Controls;
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

        // A typed grade lives in the cell editor until it is committed; the navigator commits it before it asks
        // whether the page may be left, so it is saved or flagged as unsaved rather than lost.
        vm.CommitPendingInput = CommitPendingCellEdit;
    }

    /// <summary>Commits the grid's cell or row edit in progress; false when the grid rejected the edit.</summary>
    private bool CommitPendingCellEdit() => SheetGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

    /// <summary>The view model that drives this page.</summary>
    public GradeSheetViewModel ViewModel { get; }
}
