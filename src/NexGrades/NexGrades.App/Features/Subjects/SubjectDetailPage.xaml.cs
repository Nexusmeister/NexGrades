using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Features.Subjects;

/// <summary>
/// Interaction logic for SubjectDetailPage.xaml
/// </summary>
public partial class SubjectDetailPage : INavigableView<SubjectDetailViewModel>
{
    public SubjectDetailPage(SubjectDetailViewModel vm)
    {
        ViewModel = vm;
        DataContext = this;

        InitializeComponent();
    }

    public SubjectDetailViewModel ViewModel { get; }
}