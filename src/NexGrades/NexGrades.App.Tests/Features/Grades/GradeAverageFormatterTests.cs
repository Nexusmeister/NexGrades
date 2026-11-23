using System.Globalization;
using NexGrades.App.Features.Grades;

namespace NexGrades.App.Tests.Features.Grades;

public class GradeAverageFormatterTests
{
    [Fact]
    public void Format_UsesDecimalPoint_ForEnUsCulture()
    {
        Assert.Equal("2.35", FormatIn("en-US", 2.35m));
    }

    [Fact]
    public void Format_UsesDecimalComma_ForDeDeCulture()
    {
        Assert.Equal("2,35", FormatIn("de-DE", 2.35m));
    }

    [Fact]
    public void Format_ReturnsEnDash_ForMissingAverage()
    {
        Assert.Equal("–", FormatIn("en-US", null));
    }

    [Fact]
    public void Format_ReturnsEnDash_ForMissingAverageInDeDeCulture()
    {
        Assert.Equal("–", FormatIn("de-DE", null));
    }

    [Fact]
    public void Format_ReturnsZeroAsNumber_NotAsMissing()
    {
        var text = FormatIn("en-US", 0m);

        Assert.Equal("0.00", text);
        Assert.NotEqual(GradeAverageFormatter.Missing, text);
    }

    [Fact]
    public void Format_KeepsTwoDecimals_ForWholeNumber()
    {
        Assert.Equal("3.00", FormatIn("en-US", 3m));
    }

    [Fact]
    public void Format_ShowsBestPossibleAverage_AsZeroSeventy()
    {
        Assert.Equal("0.70", FormatIn("en-US", 0.7m));
    }

    [Fact]
    public void Format_RoundsRepeatingDecimal_ToTwoDecimals()
    {
        Assert.Equal("2.33", FormatIn("en-US", 2.3333333333333333333333333333m));
    }

    [Fact]
    public void Format_RoundsHalfUp_AtThirdDecimal()
    {
        Assert.Equal("2.35", FormatIn("en-US", 2.345m));
    }

    [Fact]
    public void Format_RoundsHalfUp_ForTwoFourNineFive()
    {
        Assert.Equal("2.50", FormatIn("en-US", 2.495m));
    }

    private static string FormatIn(string cultureName, decimal? average)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        try
        {
            return GradeAverageFormatter.Format(average);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
