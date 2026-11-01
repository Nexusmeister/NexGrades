using System.Windows.Controls;

namespace NexGrades.App.Features.Home;

/// <summary>
/// Interaction logic for HomePage.xaml
/// </summary>
public partial class HomePage : Page
{
    /// <summary>Creates the page and binds <paramref name="vm"/> as its data context.</summary>
    /// <param name="vm">The view model that provides the greeting.</param>
    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
