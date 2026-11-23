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
    [InlineData("en", "Settings_Language_Header", "Language")]
    [InlineData("de", "Settings_Language_Header", "Sprache")]
    [InlineData("de-AT", "Settings_Language_SaveFailed_Title", "Fehler")]
    public void Get_ReturnsTextOfCulture_ForSettingsKey(string culture, string key, string expected) =>
        Assert.Equal(expected, TextResources.Get(key, CultureInfo.GetCultureInfo(culture)));

    [Theory]
    [InlineData("Common_Missing")]
    [InlineData("Nope_Save")]
    [InlineData("NoSeparator")]
    [InlineData("_Save")]
    [InlineData("Common_")]
    public void Get_Throws_ForUnknownKey(string key) =>
        Assert.Throws<InvalidOperationException>(() => TextResources.Get(key, CultureInfo.InvariantCulture));

    [Theory]
    [InlineData("Common_Missing_Zebra42")]
    [InlineData("Nope_Zebra42")]
    public void Get_ThrowsMessageNamingOnlyTheKey_ForUnknownKey(string key)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => TextResources.Get(key, CultureInfo.GetCultureInfo("de")));

        Assert.Contains($"'{key}'", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("'de'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Get_Throws_ForNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => TextResources.Get(null!, CultureInfo.InvariantCulture));
        Assert.Throws<ArgumentNullException>(() => TextResources.Get("Common_Save", null!));
    }

    [Fact]
    public void Text_ReadsCurrentUiCulture()
    {
        using var _ = new CultureScope(ui: "de", format: "en-US");

        Assert.Equal("Speichern", TextResources.Text("Common_Save"));
    }

    [Fact]
    public void Format_ReadsTextInUiCulture_NotInFormatCulture()
    {
        using var _ = new CultureScope(ui: "de", format: "en-GB");

        Assert.Equal("Speichern", TextResources.Format("Common_Save", 2.5m));
    }

    [Fact]
    public void Format_ReadsEnglishText_WhenUiCultureIsEnglishAndFormatIsGerman()
    {
        using var _ = new CultureScope(ui: "en", format: "de-DE");

        Assert.Equal("Save", TextResources.Format("Common_Save", 2.5m));
    }

    [Fact]
    public void Format_Throws_ForUnknownKey()
    {
        Assert.Throws<InvalidOperationException>(() => TextResources.Format("Common_Missing", 1));
    }

    /// <summary>Sets the current UI and format culture of the calling thread and restores them on dispose.</summary>
    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _oldUi = CultureInfo.CurrentUICulture;
        private readonly CultureInfo _oldFormat = CultureInfo.CurrentCulture;

        public CultureScope(string ui, string format)
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(ui);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(format);
        }

        public void Dispose()
        {
            CultureInfo.CurrentUICulture = _oldUi;
            CultureInfo.CurrentCulture = _oldFormat;
        }
    }
}
