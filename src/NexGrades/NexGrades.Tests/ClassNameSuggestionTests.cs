using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class ClassNameSuggestionTests
{
    [Theory]
    [InlineData("5a", "6a")]
    [InlineData("5A", "6A")]
    [InlineData("10b", "11b")]
    [InlineData("9c", "10c")]
    [InlineData("5", "6")]
    [InlineData(" 7a ", "8a")]
    [InlineData("Q1", "")]
    [InlineData("Klasse 5a", "")]
    [InlineData("", "")]
    public void SuggestNextName_ReturnsExpected_ForName(string current, string expected)
    {
        Assert.Equal(expected, ClassNameSuggestion.SuggestNextName(current));
    }
}
