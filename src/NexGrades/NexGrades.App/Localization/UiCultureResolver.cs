using System.Globalization;

namespace NexGrades.App.Localization;

/// <summary>Decides the cultures to run with from the saved language and the Windows cultures. It has no side effects.</summary>
public static class UiCultureResolver
{
    /// <summary>
    /// Resolves the cultures. <see cref="LanguagePreference.System"/> keeps both Windows cultures: German text then
    /// arrives by resource fallback when the Windows language is German, and formats follow the regional format. A
    /// chosen language sets the UI culture to the neutral <c>en</c> or <c>de</c>, so resource fallback works, and keeps
    /// the Windows regional culture for formats when it has the same language (so de-AT, de-CH, en-GB and the user's
    /// own overrides survive); otherwise formats use de-DE or en-GB.
    /// </summary>
    /// <param name="preference">The saved language; an undefined value is treated as <see cref="LanguagePreference.System"/>.</param>
    /// <param name="windowsUi">The Windows display language.</param>
    /// <param name="windowsFormat">The Windows regional format.</param>
    /// <returns>The UI and format cultures.</returns>
    public static UiCultures Resolve(LanguagePreference preference, CultureInfo windowsUi, CultureInfo windowsFormat)
    {
        ArgumentNullException.ThrowIfNull(windowsUi);
        ArgumentNullException.ThrowIfNull(windowsFormat);

        var (language, fallbackFormat) = preference switch
        {
            LanguagePreference.English => ("en", "en-GB"),
            LanguagePreference.German => ("de", "de-DE"),
            _ => ((string?)null, (string?)null),
        };

        if (language is null || fallbackFormat is null)
        {
            return new UiCultures(windowsUi, windowsFormat);
        }

        var format = string.Equals(windowsFormat.TwoLetterISOLanguageName, language, StringComparison.OrdinalIgnoreCase)
            ? windowsFormat
            : CultureInfo.GetCultureInfo(fallbackFormat);
        return new UiCultures(CultureInfo.GetCultureInfo(language), format);
    }
}
