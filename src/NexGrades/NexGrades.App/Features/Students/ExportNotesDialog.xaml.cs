using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Students;

/// <summary>
/// Asks for the date range and the archived-notes option of a notes export. The Export button stays visible, because
/// the WPF-UI template hides a disabled primary button. The range is checked on click instead, and the reason it is
/// refused is shown in the dialog.
/// </summary>
public partial class ExportNotesDialog : ContentDialog
{
    private readonly ExportNotesDialogViewModel _viewModel;

    /// <summary>Creates the dialog on top of the given dialog host.</summary>
    /// <param name="dialogHost">The shell's dialog host.</param>
    /// <param name="viewModel">The input state the dialog edits.</param>
    public ExportNotesDialog(ContentDialogHost? dialogHost, ExportNotesDialogViewModel viewModel)
        : base(dialogHost)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnButtonClick(ContentDialogButton button)
    {
        // Keep the dialog open and show the reason when the range is not valid.
        if (button == ContentDialogButton.Primary && !_viewModel.Revalidate())
        {
            return;
        }

        base.OnButtonClick(button);
    }
}
