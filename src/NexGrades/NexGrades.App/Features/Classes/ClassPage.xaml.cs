using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Features.Classes;

/// <summary>
/// Interaction logic for ClassPage.xaml
/// </summary>
public partial class ClassPage : INavigableView<ClassViewModel>
{
    /// <summary>Creates the page, which the navigation service resolves from DI together with its view model.</summary>
    /// <param name="vm">The view model that drives this page.</param>
    public ClassPage(ClassViewModel vm)
    {
        ViewModel = vm;
        DataContext = this;

        InitializeComponent();
    }

    /// <summary>The view model that drives this page.</summary>
    public ClassViewModel ViewModel { get; }
}
