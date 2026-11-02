using System.Globalization;

namespace NexGrades.Domain.Services;

/// <summary>Proposes the name a class gets in the next school year.</summary>
public static class ClassNameSuggestion
{
    /// <summary>
    /// Increments the leading number of <paramref name="currentName"/> and keeps the rest ("5a" becomes "6a",
    /// "10 b" becomes "11 b"). Returns an empty string when the name does not start with a number.
    /// </summary>
    /// <param name="currentName">The class name in the closing year.</param>
    public static string SuggestNextName(string currentName)
    {
        var name = currentName?.Trim() ?? string.Empty;
        var digitCount = 0;
        while (digitCount < name.Length && char.IsAsciiDigit(name[digitCount]))
        {
            digitCount++;
        }

        if (digitCount == 0 || !int.TryParse(name.AsSpan(0, digitCount), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number == int.MaxValue)
        {
            return string.Empty;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{number + 1}{name.AsSpan(digitCount)}");
    }
}
