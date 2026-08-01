using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Grading;

namespace NexGrades.Tests;

public class GradingScaleTests
{
    [Theory]
    [InlineData(0.7)]
    [InlineData(1.0)]
    [InlineData(3.3)]
    [InlineData(6.0)]
    public void IsLegalValue_ReturnsTrue_ForOneOfTheSixteenPoints(decimal value)
    {
        Assert.True(GradingScale.IsLegalValue(value));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(2.5)]
    [InlineData(7.0)]
    [InlineData(-1.0)]
    public void IsLegalValue_ReturnsFalse_ForValueNotOnTheScale(decimal value)
    {
        Assert.False(GradingScale.IsLegalValue(value));
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

    [Theory]
    [InlineData("1", 1.0)]
    [InlineData("2-", 2.3)]
    [InlineData("6", 6.0)]
    public void ValueForToken_ConvertsToken_ToItsNumericValue(string token, decimal expected)
    {
        Assert.Equal(expected, GradingScale.ValueForToken(token));
    }

    [Fact]
    public void ValueForToken_Throws_ForUnknownToken()
    {
        Assert.Throws<ArgumentException>(() => GradingScale.ValueForToken("7"));
    }

    [Theory]
    [InlineData(1.0, "1")]
    [InlineData(2.3, "2-")]
    public void TokenForValue_ConvertsValue_ToItsDisplayToken(decimal value, string expected)
    {
        Assert.Equal(expected, GradingScale.TokenForValue(value));
    }

    [Fact]
    public void TryGetValueForToken_ReturnsFalse_ForUnknownToken()
    {
        var found = GradingScale.TryGetValueForToken("unknown", out var value);

        Assert.False(found);
        Assert.Equal(0m, value);
    }
}
