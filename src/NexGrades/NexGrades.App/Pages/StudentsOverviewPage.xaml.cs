using NexGrades.App.ViewModels;
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
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Pages
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
