using System.Globalization;
using NexGrades.App.Localization;

namespace NexGrades.App.Tests.Localization;

public sealed class TextResourcesTests
{
    [Theory]
    [InlineData("en", "Common_Save", "Save")]
    [InlineData("de", "Common_Save", "Speichern")]
    [InlineData("de", "Common_Back", "Zurück")]
    [InlineData("fr", "Common_Cancel", "Cancel")]
    public void Get_ReturnsTextOfCulture_ForKnownKey(string culture, string key, string expected) =>
        Assert.Equal(expected, TextResources.Get(key, CultureInfo.GetCultureInfo(culture)));

    [Theory]
    [InlineData("Common_Missing")]
    [InlineData("Nope_Save")]
    [InlineData("NoSeparator")]
    public void Get_Throws_ForUnknownKey(string key) =>
        Assert.Throws<InvalidOperationException>(() => TextResources.Get(key, CultureInfo.InvariantCulture));
}
