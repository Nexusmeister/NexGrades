namespace NexGrades.App.Core.YearLock;

/// <summary>
/// Shows whether a school year is locked or temporarily unlocked, with the button to change that. Its
/// <c>DataContext</c> is a <see cref="YearLockViewModel"/>; a page uses it as
/// <c>&lt;yearLock:YearLockBar DataContext="{Binding ViewModel.YearLock}"/&gt;</c>. It takes no space when the
/// year is not closed.
/// </summary>
public partial class YearLockBar : System.Windows.Controls.UserControl
{
    /// <summary>Creates the control.</summary>
    public YearLockBar()
    {
        InitializeComponent();
    }
}
