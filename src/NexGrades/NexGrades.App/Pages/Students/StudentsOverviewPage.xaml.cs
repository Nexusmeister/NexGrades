using NexGrades.App.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Pages.Students
{
    /// <summary>
    /// Interaction logic for StudentsOverviewPage.xaml
    /// </summary>
    public partial class StudentsOverviewPage : INavigableView<StudentsOverviewViewModel>
    {
        public StudentsOverviewPage(StudentsOverviewViewModel vm)
        {
            ViewModel = vm;
            DataContext = this;

            InitializeComponent();
        }

        public StudentsOverviewViewModel ViewModel { get; }
    }
}
