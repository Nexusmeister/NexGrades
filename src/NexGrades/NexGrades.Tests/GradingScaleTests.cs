using System.Globalization;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Grading;

namespace NexGrades.Tests;

public class GradingScaleTests
{
    // Decimal theory data is passed as invariant strings: a double literal would be converted to decimal exactly
    // (0.7 becomes 0.6999999999999999555910790150 since .NET 11), which is not the grade the test means.
    private static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("0.7")]
    [InlineData("1.0")]
    [InlineData("3.3")]
    [InlineData("6.0")]
    public void IsLegalValue_ReturnsTrue_ForOneOfTheSixteenPoints(string value)
    {
        Assert.True(GradingScale.IsLegalValue(Parse(value)));
    }

    [Theory]
    [InlineData("0.0")]
    [InlineData("2.5")]
    [InlineData("7.0")]
    [InlineData("-1.0")]
    public void IsLegalValue_ReturnsFalse_ForValueNotOnTheScale(string value)
    {
        Assert.False(GradingScale.IsLegalValue(Parse(value)));
    }

    [Fact]
    public void Validate_Throws_ForIllegalValue()
    {
        Assert.Throws<InvariantViolationException>(() => GradingScale.Validate(2.5m));
    }

    [Fact]
    public void Validate_DoesNotThrow_ForLegalValue()
    {
        var exception = Record.Exception(() => GradingScale.Validate(2.0m));
        Assert.Null(exception);
    }

    [Fact]
    public void TokenForValue_Throws_ForIllegalValue()
    {
        Assert.Throws<InvariantViolationException>(() => GradingScale.TokenForValue(2.5m));
    }

    [Theory]
    [InlineData("1", "1.0")]
    [InlineData("2-", "2.3")]
    [InlineData("6", "6.0")]
    public void ValueForToken_ConvertsToken_ToItsNumericValue(string token, string expected)
    {
        Assert.Equal(Parse(expected), GradingScale.ValueForToken(token));
    }

    [Fact]
    public void ValueForToken_Throws_ForUnknownToken()
    {
        Assert.Throws<InvariantViolationException>(() => GradingScale.ValueForToken("7"));
    }

    [Theory]
    [InlineData("1.0", "1")]
    [InlineData("2.3", "2-")]
    public void TokenForValue_ConvertsValue_ToItsDisplayToken(string value, string expected)
    {
        Assert.Equal(expected, GradingScale.TokenForValue(Parse(value)));
    }

    [Fact]
    public void TryGetValueForToken_ReturnsFalse_ForUnknownToken()
    {
        var found = GradingScale.TryGetValueForToken("unknown", out var value);

        Assert.False(found);
        Assert.Equal(0m, value);
    }

    [Theory]
    [InlineData("2-", "2.3")]
    [InlineData(" 1+ ", "0.7")]
    [InlineData("2 -", "2.3")]
    [InlineData("6", "6.0")]
    public void TryParseToken_ReturnsValue_ForLegalTokenIgnoringWhitespace(string input, string expected)
    {
        var parsed = GradingScale.TryParseToken(input, out var value);

        Assert.True(parsed);
        Assert.Equal(Parse(expected), value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("7")]
    [InlineData("0")]
    [InlineData("2.3")]
    [InlineData("2--")]
    [InlineData("abc")]
    public void TryParseToken_ReturnsFalse_ForIllegalInput(string? input)
    {
        var parsed = GradingScale.TryParseToken(input, out var value);

        Assert.False(parsed);
        Assert.Equal(0m, value);
    }
}
