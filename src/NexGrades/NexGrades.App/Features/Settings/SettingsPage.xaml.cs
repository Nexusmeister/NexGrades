using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Features.Settings
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
