using NexGrades.App.Features.Export;

namespace NexGrades.App.Tests;

public class ExcelSheetNameTests
{
    [Fact]
    public void Sanitize_JoinsSubjectClassAndYear_ForShortNames()
    {
        Assert.Equal("Testkunde 5A 2025-26", ExcelSheetName.Sanitize("Testkunde", "5A", "2025/26"));
    }

    [Theory]
    [InlineData(':')]
    [InlineData('\\')]
    [InlineData('/')]
    [InlineData('?')]
    [InlineData('*')]
    [InlineData('[')]
    [InlineData(']')]
    public void Sanitize_ReplacesForbiddenCharacter_WithDash(char forbidden)
    {
        var name = ExcelSheetName.Sanitize($"A{forbidden}B", "5A", "2025");

        Assert.Equal("A-B 5A 2025", name);
    }

    [Fact]
    public void Sanitize_TruncatesSubjectFirst_KeepingClassAndYear()
    {
        var name = ExcelSheetName.Sanitize(new string('x', 60), "5A", "2025-26");

        Assert.Equal(ExcelSheetName.MaxLength, name.Length);
        Assert.EndsWith(" 5A 2025-26", name, StringComparison.Ordinal);
        Assert.StartsWith("xxx", name, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_StaysWithinMaxLength_WhenClassAndYearAreVeryLong()
    {
        var name = ExcelSheetName.Sanitize("Sub", new string('c', 40), new string('y', 40));

        Assert.True(name.Length <= ExcelSheetName.MaxLength);
    }

    [Fact]
    public void Sanitize_TrimsLeadingAndTrailingApostrophes()
    {
        var name = ExcelSheetName.Sanitize("'Sub", "5A", "'25'");

        Assert.False(name.StartsWith('\''));
        Assert.False(name.EndsWith('\''));
    }

    [Fact]
    public void Sanitize_ReturnsFallback_WhenNothingUsableIsLeft()
    {
        Assert.Equal(ExcelSheetName.Fallback, ExcelSheetName.Sanitize("'", "", "'"));
    }

    [Fact]
    public void Sanitize_ReturnsFallback_ForReservedNameHistory()
    {
        Assert.Equal(ExcelSheetName.Fallback, ExcelSheetName.Sanitize("History", "", ""));
    }

    [Fact]
    public void Sanitize_ReturnsFallback_ForReservedNameInOtherCasing()
    {
        Assert.Equal(ExcelSheetName.Fallback, ExcelSheetName.Sanitize("hIsToRy", "", ""));
    }
}
