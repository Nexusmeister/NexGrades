using Wpf.Ui.Controls;

namespace NexGrades.App.Core.YearLock;

/// <summary>
/// Asks for the reason and the duration when unlocking a closed school year. The Unlock button is enabled only
/// while the input is valid, and is checked again on click, because "in the future" changes while the dialog is open.
/// </summary>
public partial class UnlockYearDialog : ContentDialog
{
    private readonly UnlockYearDialogViewModel _viewModel;

    /// <summary>Creates the dialog on top of the given dialog host.</summary>
    /// <param name="dialogHost">The shell's dialog host.</param>
    /// <param name="viewModel">The input state the dialog edits.</param>
    public UnlockYearDialog(ContentDialogHost? dialogHost, UnlockYearDialogViewModel viewModel)
        : base(dialogHost)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnButtonClick(ContentDialogButton button)
    {
        // Keep the dialog open and show the reason when the input has become invalid.
        if (button == ContentDialogButton.Primary && !_viewModel.Revalidate())
        {
            return;
        }

        base.OnButtonClick(button);
    }
}
