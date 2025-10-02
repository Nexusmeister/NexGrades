using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NexGrades.App.ViewModels;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace NexGrades.App.Pages
{
    public partial class ClassesOverviewPage : INavigableView<ClassesOverviewViewModel>
    {
        public ClassesOverviewPage(ClassesOverviewViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        public ClassesOverviewViewModel ViewModel { get; }
    }
}
