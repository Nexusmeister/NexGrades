using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Features.Students
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
