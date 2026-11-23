using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Globalization;
using System.Resources;

namespace NexGrades.App.Localization;

/// <summary>
/// Finds localized UI text by key. A key is <c>&lt;Area&gt;_&lt;Context&gt;_&lt;Name&gt;[_Role]</c>, for example
/// <c>Settings_Language_Header</c>: the part before the first underscore names the resource set (the area's
/// <c>&lt;Area&gt;Strings.resx</c>), and the whole key is the entry's name inside it. XAML and C# both go through this
/// lookup, because WPF compiles markup before MSBuild generates strongly typed resource classes, so those are not
/// visible to either.
/// </summary>
/// <remarks>
/// English is the neutral resource; German is the <c>.de.resx</c> satellite. A key missing from the neutral resource
/// is a programming error and throws, so a typo cannot ship as a blank or bracketed label. A key missing only from
/// German falls back to English; the tests check that every German file is complete.
/// </remarks>
public static class TextResources
{
    // One line per area; a new area's resx is added here. The value is the manifest base name of its resx
    // (root namespace + folder + file name without extension).
    private static readonly FrozenDictionary<string, string> _baseNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Common"] = "NexGrades.App.Localization.CommonStrings",
        ["Core"] = "NexGrades.App.Core.CoreStrings",
        ["Home"] = "NexGrades.App.Features.Home.HomeStrings",
        ["Shell"] = "NexGrades.App.Shell.ShellStrings",
        ["Settings"] = "NexGrades.App.Features.Settings.SettingsStrings",
        ["Classes"] = "NexGrades.App.Features.Classes.ClassesStrings",
        ["Students"] = "NexGrades.App.Features.Students.StudentsStrings",
        ["Subjects"] = "NexGrades.App.Features.Subjects.SubjectsStrings",
        ["Grades"] = "NexGrades.App.Features.Grades.GradesStrings",
        ["SchoolYears"] = "NexGrades.App.Features.SchoolYears.SchoolYearsStrings",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly ConcurrentDictionary<string, ResourceManager> _managers = new(StringComparer.Ordinal);

    /// <summary>The areas that have a resource set, which tests walk to check that German is complete.</summary>
    internal static IReadOnlyCollection<string> Areas => _baseNames.Keys;

    /// <summary>Returns the resource manager of an area, for tests that enumerate its entries.</summary>
    /// <param name="area">An area name from <see cref="Areas"/>.</param>
    internal static ResourceManager GetManager(string area) => _managers.GetOrAdd(area, static a =>
        new ResourceManager(_baseNames[a], typeof(TextResources).Assembly));

    /// <summary>Looks up a key in a given culture.</summary>
    /// <param name="key">The key, area first.</param>
    /// <param name="culture">The UI culture to read the text for; a culture without its own text falls back to English.</param>
    /// <returns>The localized text.</returns>
    /// <exception cref="InvalidOperationException">The key is malformed, its area is not registered, or the neutral
    /// resource has no such entry. The message names only the key.</exception>
    public static string Get(string key, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);

        var separator = key.IndexOf('_', StringComparison.Ordinal);
        if (separator <= 0 || separator == key.Length - 1 || !_baseNames.ContainsKey(key[..separator]))
        {
            throw new InvalidOperationException($"The text key '{key}' does not name a registered area.");
        }

        string? text;
        try
        {
            text = GetManager(key[..separator]).GetString(key, culture);
        }
        catch (MissingManifestResourceException)
        {
            text = null;
        }

        return text ?? throw new InvalidOperationException($"The text key '{key}' is missing.");
    }

    /// <summary>Looks up a key in <see cref="CultureInfo.CurrentUICulture"/>.</summary>
    /// <param name="key">The key, area first.</param>
    /// <returns>The localized text.</returns>
    public static string Text(string key) => Get(key, CultureInfo.CurrentUICulture);

    /// <summary>
    /// Looks up a format string and fills it in. The text is read in <see cref="CultureInfo.CurrentUICulture"/> (the
    /// language), while numbers, dates and averages in <paramref name="args"/> are formatted in
    /// <see cref="CultureInfo.CurrentCulture"/> (the Windows regional format), so a German-language user with a
    /// British region still sees dates the way the region writes them.
    /// </summary>
    /// <param name="key">The key of a whole format string with placeholders, never a sentence fragment.</param>
    /// <param name="args">The values for the placeholders.</param>
    /// <returns>The formatted text.</returns>
    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Text(key), args);
}
