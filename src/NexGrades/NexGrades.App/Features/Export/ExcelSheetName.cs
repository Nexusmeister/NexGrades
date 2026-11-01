namespace NexGrades.App.Features.Export;

/// <summary>
/// Builds a worksheet name that Excel accepts. Excel limits names to 31 characters, forbids
/// <c>: \ / ? * [ ]</c>, forbids a leading or trailing apostrophe and reserves the name "History".
/// </summary>
public static class ExcelSheetName
{
    /// <summary>Excel's maximum worksheet name length.</summary>
    public const int MaxLength = 31;

    /// <summary>The name used when nothing usable is left, and in place of the reserved name.</summary>
    public const string Fallback = "Export";

    private const string ReservedName = "History";

    private static readonly char[] ForbiddenCharacters = [':', '\\', '/', '?', '*', '[', ']'];

    /// <summary>
    /// Returns "<paramref name="subject"/> <paramref name="className"/> <paramref name="year"/>" made safe for Excel.
    /// When the name is too long the subject is shortened first, so the class and the year stay readable.
    /// </summary>
    public static string Sanitize(string subject, string className, string year)
    {
        var cleanSubject = Clean(subject);
        var suffix = $" {Clean(className)} {Clean(year)}";

        string name;
        if (cleanSubject.Length + suffix.Length <= MaxLength)
        {
            name = cleanSubject + suffix;
        }
        else if (suffix.Length < MaxLength)
        {
            name = cleanSubject[..(MaxLength - suffix.Length)] + suffix;
        }
        else
        {
            name = (cleanSubject + suffix)[..MaxLength];
        }

        name = name.Trim().Trim('\'').Trim();

        return name.Length == 0 || string.Equals(name, ReservedName, StringComparison.OrdinalIgnoreCase)
            ? Fallback
            : name;
    }

    private static string Clean(string value)
    {
        var chars = value.Trim().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(ForbiddenCharacters, chars[i]) >= 0 || char.IsControl(chars[i]))
            {
                chars[i] = '-';
            }
        }

        return new string(chars);
    }
}
