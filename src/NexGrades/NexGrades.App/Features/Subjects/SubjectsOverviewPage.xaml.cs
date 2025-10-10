using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Features.Subjects;

/// <summary>
/// Interaction logic for SubjectsOverviewPage.xaml
/// </summary>
public partial class SubjectsOverviewPage : INavigableView<SubjectsOverviewViewModel>
{
    public SubjectsOverviewPage(SubjectsOverviewViewModel vm)
    {
        ViewModel = vm;
        DataContext = this;

        InitializeComponent();
    }

    public SubjectsOverviewViewModel ViewModel { get; }
}