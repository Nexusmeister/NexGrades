using NexGrades.App.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Pages
{
    public partial class ClassesOverviewPage : INavigableView<ClassesOverviewViewModel>
    {
        public ClassesOverviewPage(ClassesOverviewViewModel vm)
        {
            ViewModel = vm;
            DataContext = this;

            InitializeComponent();
        }

        public ClassesOverviewViewModel ViewModel { get; } 
    }
}
