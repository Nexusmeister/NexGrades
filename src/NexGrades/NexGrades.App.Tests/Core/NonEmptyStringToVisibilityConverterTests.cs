using System.Globalization;
using System.Windows;
using NexGrades.App.Core;

namespace NexGrades.App.Tests.Core;

public class NonEmptyStringToVisibilityConverterTests
{
    private readonly NonEmptyStringToVisibilityConverter _converter = new();

    [Theory]
    [InlineData("text")]
    [InlineData(" ")]
    public void Convert_ReturnsVisible_ForNonEmptyString(string value)
    {
        Assert.Equal(Visibility.Visible, Convert(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Convert_ReturnsCollapsed_ForNullOrEmptyString(string? value)
    {
        Assert.Equal(Visibility.Collapsed, Convert(value));
    }

    [Fact]
    public void Convert_ReturnsCollapsed_ForNonStringValue()
    {
        Assert.Equal(Visibility.Collapsed, Convert(42));
    }

    [Fact]
    public void ConvertBack_Throws_NotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() =>
            _converter.ConvertBack(Visibility.Visible, typeof(string), null!, CultureInfo.InvariantCulture));
    }

    private object Convert(object? value) =>
        _converter.Convert(value!, typeof(Visibility), null!, CultureInfo.InvariantCulture);
}
