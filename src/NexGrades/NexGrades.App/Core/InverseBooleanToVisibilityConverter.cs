using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NexGrades.App.Core;

/// <summary>
/// Maps <c>true</c> to <see cref="Visibility.Collapsed"/> and <c>false</c> to <see cref="Visibility.Visible"/>,
/// so XAML can show something while a flag is off without a dedicated negated view-model property.
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    /// <summary>Returns <see cref="Visibility.Collapsed"/> for <c>true</c>, and <see cref="Visibility.Visible"/> for any other value.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Returns <c>true</c> for <see cref="Visibility.Collapsed"/> and <c>false</c> for any other value.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}
