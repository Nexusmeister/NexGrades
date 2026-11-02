using System.Globalization;
using System.Windows.Markup;
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Features.SchoolYears;

/// <summary>
/// The guided "End school year" flow.
/// </summary>
public partial class SchoolYearPage : INavigableView<SchoolYearViewModel>
{
    /// <summary>Creates the page, which the navigation service resolves from DI together with its view model.</summary>
    /// <param name="vm">The view model that drives this page.</param>
    public SchoolYearPage(SchoolYearViewModel vm)
    {
        ViewModel = vm;
        DataContext = this;

        // XAML bindings format dates in en-US unless the element's language says otherwise.
        Language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);

        InitializeComponent();
    }

    /// <summary>The view model that drives this page.</summary>
    public SchoolYearViewModel ViewModel { get; }
}
