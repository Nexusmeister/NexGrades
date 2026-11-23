using System.Globalization;
using System.Windows.Markup;

namespace NexGrades.App.Localization;

/// <summary>
/// XAML access to localized text: <c>{loc:Text Common_Save}</c>. Resolved once, when the XAML loads.
/// </summary>
/// <param name="key">The key, prefix first (see <see cref="TextResources"/>).</param>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TextExtension(string key) : MarkupExtension
{
    /// <summary>The key, prefix first.</summary>
    public string Key { get; } = key;

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider) => TextResources.Get(Key, CultureInfo.CurrentUICulture);
}
