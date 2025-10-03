using System.Windows.Controls;

namespace NexGrades.App.Features.Home
{
    /// <summary>
    /// Interaction logic for HomePage.xaml
    /// </summary>
    public partial class HomePage : Page
    {
        public HomePage(HomeViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
