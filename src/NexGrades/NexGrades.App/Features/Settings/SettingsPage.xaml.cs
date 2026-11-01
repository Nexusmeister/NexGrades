using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Features.Settings;

/// <summary>
/// Interaction logic for SettingsPage.xaml
/// </summary>
public partial class SettingsPage : INavigableView<SettingsViewModel>
{
    /// <summary>Creates the page, which the navigation service resolves from DI together with its view model.</summary>
    /// <param name="vm">The view model that drives this page.</param>
    public SettingsPage(SettingsViewModel vm)
    {
        ViewModel = vm;
        DataContext = this;

        InitializeComponent();
    }

    /// <summary>The view model that drives this page.</summary>
    public SettingsViewModel ViewModel { get; }
}
