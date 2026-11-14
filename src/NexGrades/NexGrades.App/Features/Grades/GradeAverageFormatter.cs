using System.Globalization;

namespace NexGrades.App.Features.Grades;

/// <summary>Formats averages for the grade sheet with the current UI culture.</summary>
public static class GradeAverageFormatter
{
    /// <summary>The text shown for an average that does not exist (an en dash).</summary>
    public const string Missing = "–";

    /// <summary>
    /// Formats <paramref name="average"/> with two decimals in the current culture, for example <c>2,35</c> for
    /// German. An empty bucket or subject has no average and shows <see cref="Missing"/>, never zero.
    /// </summary>
    /// <param name="average">The average, or <see langword="null"/> when there is none.</param>
    /// <returns>The text to display.</returns>
    public static string Format(decimal? average) =>
        average is { } value ? value.ToString("0.00", CultureInfo.CurrentCulture) : Missing;
}
