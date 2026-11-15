using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NexGrades.App.Core;

/// <summary>
/// Maps a null or empty string to <see cref="Visibility.Collapsed"/> and any other string to
/// <see cref="Visibility.Visible"/>. It is bound directly to <c>Visibility</c> because WPF-UI replaces the style of a
/// <c>ui:TextBlock</c> at runtime, so a style that collapses an empty text never applies.
/// </summary>
[ValueConversion(typeof(string), typeof(Visibility))]
public sealed class NonEmptyStringToVisibilityConverter : IValueConverter
{
    /// <summary>Returns <see cref="Visibility.Visible"/> for a non-empty string, and <see cref="Visibility.Collapsed"/> otherwise.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Not supported; the conversion is one way.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
