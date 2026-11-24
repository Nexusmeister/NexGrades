using System.Windows.Controls;

namespace NexGrades.App.Features.Home;

/// <summary>
/// Interaction logic for HomePage.xaml
/// </summary>
public partial class HomePage : Page
{
    /// <summary>Creates the page and binds <paramref name="vm"/> as its data context.</summary>
    /// <param name="vm">The view model that provides the greeting and the year-end reminder.</param>
    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // The page is not an INavigableView, so the navigation hook may not reach its view model. Loaded fires on each
        // visit of this singleton page and refreshes the reminder either way; a second refresh is harmless.
        Loaded += (_, _) => _ = vm.RefreshYearEndReminderAsync();
    }
}
