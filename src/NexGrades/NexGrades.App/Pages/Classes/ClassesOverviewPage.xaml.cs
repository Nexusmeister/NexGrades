using NexGrades.App.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Pages.Classes
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
