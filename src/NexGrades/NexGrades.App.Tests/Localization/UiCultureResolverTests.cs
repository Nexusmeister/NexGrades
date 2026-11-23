using System.Globalization;
using NexGrades.App.Localization;

namespace NexGrades.App.Tests.Localization;

public sealed class UiCultureResolverTests
{
    private static UiCultures Resolve(LanguagePreference preference, string windowsUi, string windowsFormat) =>
        UiCultureResolver.Resolve(preference, CultureInfo.GetCultureInfo(windowsUi), CultureInfo.GetCultureInfo(windowsFormat));

    [Fact]
    public void Resolve_KeepsBothWindowsCultures_ForSystemOnGermanWindows()
    {
        var cultures = Resolve(LanguagePreference.System, "de-DE", "de-DE");

        Assert.Equal("de-DE", cultures.Ui.Name);
        Assert.Equal("de-DE", cultures.Format.Name);
    }

    [Fact]
    public void Resolve_KeepsUiTextCultureAndGermanFormats_ForSystemWithEnglishUiAndGermanRegion()
    {
        var cultures = Resolve(LanguagePreference.System, "en-US", "de-DE");

        Assert.Equal("en-US", cultures.Ui.Name);
        Assert.Equal("de-DE", cultures.Format.Name);
    }

    [Fact]
    public void Resolve_UsesGermanUiAndGermanFormats_ForGermanWithEnglishRegion()
    {
        var cultures = Resolve(LanguagePreference.German, "en-US", "en-US");

        Assert.Equal("de", cultures.Ui.Name);
        Assert.Equal("de-DE", cultures.Format.Name);
    }

    [Fact]
    public void Resolve_KeepsAustrianFormats_ForGermanWithAustrianRegion()
    {
        var cultures = Resolve(LanguagePreference.German, "en-US", "de-AT");

        Assert.Equal("de", cultures.Ui.Name);
        Assert.Equal("de-AT", cultures.Format.Name);
    }

    [Fact]
    public void Resolve_UsesBritishFormats_ForEnglishWithGermanRegion()
    {
        var cultures = Resolve(LanguagePreference.English, "de-DE", "de-DE");

        Assert.Equal("en", cultures.Ui.Name);
        Assert.Equal("en-GB", cultures.Format.Name);
    }

    [Fact]
    public void Resolve_KeepsBritishFormats_ForEnglishWithBritishRegion()
    {
        var cultures = Resolve(LanguagePreference.English, "de-DE", "en-GB");

        Assert.Equal("en", cultures.Ui.Name);
        Assert.Equal("en-GB", cultures.Format.Name);
    }

    [Fact]
    public void Resolve_TreatsUndefinedPreferenceAsSystem()
    {
        var cultures = Resolve((LanguagePreference)99, "en-US", "de-DE");

        Assert.Equal("en-US", cultures.Ui.Name);
        Assert.Equal("de-DE", cultures.Format.Name);
    }

    [Fact]
    public void Resolve_Throws_ForNullCultures()
    {
        Assert.Throws<ArgumentNullException>(() => UiCultureResolver.Resolve(LanguagePreference.System, null!, CultureInfo.InvariantCulture));
        Assert.Throws<ArgumentNullException>(() => UiCultureResolver.Resolve(LanguagePreference.System, CultureInfo.InvariantCulture, null!));
    }
}
