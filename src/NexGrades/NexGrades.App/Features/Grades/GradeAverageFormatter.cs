using System.Globalization;
using NexGrades.Domain.Grading;

namespace NexGrades.App.Features.Grades;

/// <summary>Formats averages and final grades for the grade sheet with the current UI culture.</summary>
public static class GradeAverageFormatter
{
    /// <summary>The text shown for an average that does not exist (an en dash).</summary>
    public const string Missing = "–";

    /// <summary>
    /// Formats <paramref name="average"/> with two decimals in the current culture, for example <c>2,35</c> for
    /// German. The value is rounded by <see cref="AverageCalculator.RoundForDisplay"/>, the same rounding the final
    /// grade proposal uses. An empty bucket or subject has no average and shows <see cref="Missing"/>, never zero.
    /// </summary>
    /// <param name="average">The average, or <see langword="null"/> when there is none.</param>
    /// <returns>The text to display.</returns>
    public static string Format(decimal? average) =>
        average is { } value
            ? AverageCalculator.RoundForDisplay(value).ToString("0.00", CultureInfo.CurrentCulture)
            : Missing;

    /// <summary>
    /// Formats a final grade as a whole number in the current culture. A final grade that does not exist gives an
    /// empty string, never zero.
    /// </summary>
    /// <param name="finalGrade">The final grade, or <see langword="null"/> when there is none.</param>
    /// <returns>The text to display, or an empty string.</returns>
    public static string FormatFinalGrade(decimal? finalGrade) =>
        finalGrade is { } value ? decimal.Truncate(value).ToString("0", CultureInfo.CurrentCulture) : string.Empty;
}
