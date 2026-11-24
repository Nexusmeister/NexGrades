using Wpf.Ui.Controls;

namespace NexGrades.App.Core.YearLock;

/// <summary>
/// Asks for the reason and the duration when unlocking a closed school year. The Unlock button stays visible, because
/// the WPF-UI template hides a disabled primary button. The input is checked on click instead ("in the future" also
/// changes while the dialog is open) and the reason it is refused is shown in the dialog.
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
