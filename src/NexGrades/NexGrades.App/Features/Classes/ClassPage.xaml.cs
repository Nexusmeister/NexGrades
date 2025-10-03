using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Features.Classes
{
    /// <summary>
    /// Interaction logic for ClassPage.xaml
    /// </summary>
    public partial class ClassPage : INavigableView<ClassViewModel>
    {
        public ClassPage(ClassViewModel vm)
        {
            ViewModel = vm;
            DataContext = this;

            InitializeComponent();
        }

        public ClassViewModel ViewModel { get; }
    }
}
