using System.Globalization;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Grading;

namespace NexGrades.Tests.Grading;

public class FinalGradeProposalTests
{
    private static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    [Fact]
    public void ProposeFinalGrade_ReturnsNull_ForNoAverage()
    {
        Assert.Null(GradingScale.ProposeFinalGrade(null));
    }

    [Theory]
    [InlineData("0.7", 1)]
    [InlineData("1.0", 1)]
    [InlineData("1.49", 1)]
    [InlineData("1.50", 2)]
    [InlineData("2.49", 2)]
    [InlineData("2.50", 3)]
    [InlineData("2.4949", 2)]
    [InlineData("2.495", 3)]
    [InlineData("5.49", 5)]
    [InlineData("5.50", 6)]
    [InlineData("6.0", 6)]
    public void ProposeFinalGrade_RoundsDisplayedAverage_HalfGoingToTheWorseGrade(string average, int expected)
    {
        Assert.Equal((decimal)expected, GradingScale.ProposeFinalGrade(Parse(average)));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("5")]
    [InlineData("6")]
    public void IsLegalFinalGrade_ReturnsTrue_ForWholeGradeFromOneToSix(string value)
    {
        Assert.True(GradingScale.IsLegalFinalGrade(Parse(value)));
    }

    [Theory]
    [InlineData("0.7")]
    [InlineData("1.5")]
    [InlineData("2.3")]
    [InlineData("0")]
    [InlineData("7")]
    public void IsLegalFinalGrade_ReturnsFalse_ForTendencyOrOutOfRangeValue(string value)
    {
        Assert.False(GradingScale.IsLegalFinalGrade(Parse(value)));
    }

    [Theory]
    [InlineData("3")]
    [InlineData(" 3 ")]
    public void TryParseFinalGradeToken_ReturnsTrue_ForWholeGradeToken(string input)
    {
        Assert.True(GradingScale.TryParseFinalGradeToken(input, out var value));
        Assert.Equal(3m, value);
    }

    [Theory]
    [InlineData("2-")]
    [InlineData("1+")]
    [InlineData("7")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParseFinalGradeToken_ReturnsFalse_ForTendencyOrInvalidInput(string? input)
    {
        Assert.False(GradingScale.TryParseFinalGradeToken(input, out var value));
        Assert.Equal(0m, value);
    }

    [Fact]
    public void ValidateFinalGrade_Throws_ForTendencyValue()
    {
        Assert.Throws<InvariantViolationException>(() => GradingScale.ValidateFinalGrade(2.3m));
    }

    [Fact]
    public void ValidateFinalGrade_DoesNotThrow_ForWholeGrade()
    {
        Assert.Null(Record.Exception(() => GradingScale.ValidateFinalGrade(4m)));
    }

    [Fact]
    public void RoundForDisplay_RoundsMidpointUp_ToTwoDecimals()
    {
        Assert.Equal(2.50m, AverageCalculator.RoundForDisplay(2.495m));
    }

    [Fact]
    public void RoundForDisplay_RoundsDown_BelowMidpoint()
    {
        Assert.Equal(2.49m, AverageCalculator.RoundForDisplay(2.494m));
    }
}
