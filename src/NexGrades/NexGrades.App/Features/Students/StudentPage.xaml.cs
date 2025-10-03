using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Features.Students
{
    /// <summary>
    /// Interaction logic for StudentPage.xaml
    /// </summary>
    public partial class StudentPage : INavigableView<StudentViewModel>
    {
        public StudentPage(StudentViewModel vm)
        {
            ViewModel = vm;
            DataContext = this;

            InitializeComponent();
        }

        public StudentViewModel ViewModel { get; }
    }
}
