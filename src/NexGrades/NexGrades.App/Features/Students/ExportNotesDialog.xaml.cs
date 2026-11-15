using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Students;

/// <summary>
/// Asks for the date range and the archived-notes option of a notes export. The Export button is enabled only
/// while the range is valid.
/// </summary>
public partial class ExportNotesDialog : ContentDialog
{
    /// <summary>Creates the dialog on top of the given dialog host.</summary>
    /// <param name="dialogHost">The shell's dialog host.</param>
    /// <param name="viewModel">The input state the dialog edits.</param>
    public ExportNotesDialog(ContentDialogHost? dialogHost, ExportNotesDialogViewModel viewModel)
        : base(dialogHost)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
