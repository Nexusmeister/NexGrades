using NexGrades.App.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Pages
{
    /// <summary>
    /// Interaction logic for SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : INavigableView<SettingsViewModel>
    {
        public SettingsPage(SettingsViewModel vm)
        {
            ViewModel = vm;
            DataContext = this;

            InitializeComponent();
        }

        public SettingsViewModel ViewModel { get; }
    }
}
