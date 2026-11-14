using NexGrades.Domain.Exceptions;

namespace NexGrades.Domain.Grading;

/// <summary>
/// The German 1–6 tendency grading scale. Grades are stored numerically (<see cref="Data.Entities.Grade.Value"/>)
/// so averages work, and displayed via this fixed token mapping. Lower is better. Validation that a value is
/// one of the 16 legal points on the scale is application logic, not a database constraint — the schema
/// stores a plain <c>decimal</c>.
/// </summary>
public static class GradingScale
{
    private static readonly Dictionary<string, decimal> TokenToValueMap = new(StringComparer.Ordinal)
    {
        ["1+"] = 0.7m,
        ["1"] = 1.0m,
        ["1-"] = 1.3m,
        ["2+"] = 1.7m,
        ["2"] = 2.0m,
        ["2-"] = 2.3m,
        ["3+"] = 2.7m,
        ["3"] = 3.0m,
        ["3-"] = 3.3m,
        ["4+"] = 3.7m,
        ["4"] = 4.0m,
        ["4-"] = 4.3m,
        ["5+"] = 4.7m,
        ["5"] = 5.0m,
        ["5-"] = 5.3m,
        ["6"] = 6.0m,
    };

    private static readonly IReadOnlyDictionary<decimal, string> ValueToTokenMap =
        TokenToValueMap.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>All 16 legal grade values on the scale, e.g. for populating a picker.</summary>
    public static IReadOnlyCollection<decimal> LegalValues { get; } = [.. ValueToTokenMap.Keys];

    /// <summary>Whether <paramref name="value"/> is one of the 16 legal points on the scale.</summary>
    public static bool IsLegalValue(decimal value) => ValueToTokenMap.ContainsKey(value);

    /// <summary>
    /// Throws <see cref="InvariantViolationException"/> if <paramref name="value"/> is not one of the 16
    /// legal grade values on the scale.
    /// </summary>
    public static void Validate(decimal value)
    {
        if (!IsLegalValue(value))
        {
            throw new InvariantViolationException(
                $"{value} is not a legal grade value. Legal values are: {string.Join(", ", LegalValues.OrderBy(v => v))}.");
        }
    }

    /// <summary>Converts a display token (e.g. "2-") to its numeric value. Throws for an unknown token.</summary>
    public static decimal ValueForToken(string token) =>
        TokenToValueMap.TryGetValue(token, out var value)
            ? value
            : throw new InvariantViolationException($"'{token}' is not a legal grade token.");

    /// <summary>Converts a numeric grade value to its display token (e.g. "2-"). Throws for an illegal value.</summary>
    public static string TokenForValue(decimal value) =>
        ValueToTokenMap.TryGetValue(value, out var token)
            ? token
            : throw new InvariantViolationException($"{value} is not a legal grade value.");

    /// <summary>Non-throwing variant of <see cref="ValueForToken"/>.</summary>
    public static bool TryGetValueForToken(string token, out decimal value) => TokenToValueMap.TryGetValue(token, out value);

    /// <summary>
    /// Parses typed input such as "2-", " 1+ " or "2 -" into its numeric value. Leading, trailing and inner
    /// whitespace is ignored; the remaining text must be exactly one of the scale's tokens. Keeps scale
    /// knowledge here so callers never parse tokens themselves.
    /// </summary>
    /// <param name="input">The raw typed text.</param>
    /// <param name="value">The grade value when parsing succeeds, otherwise 0.</param>
    /// <returns><see langword="true"/> when <paramref name="input"/> is a legal token.</returns>
    public static bool TryParseToken(string? input, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = string.Concat(input.Where(c => !char.IsWhiteSpace(c)));
        return TokenToValueMap.TryGetValue(normalized, out value);
    }

    /// <summary>Non-throwing variant of <see cref="TokenForValue"/>.</summary>
    public static bool TryGetTokenForValue(decimal value, out string? token) => ValueToTokenMap.TryGetValue(value, out token);

    /// <summary>
    /// The six legal final (certificate) grades, 1 to 6, best first. A final grade is a whole grade: it has no
    /// tendency, so "2-" is not a final grade.
    /// </summary>
    public static IReadOnlyList<decimal> FinalGradeValues { get; } = [1m, 2m, 3m, 4m, 5m, 6m];

    /// <summary>Whether <paramref name="value"/> is a whole grade from 1 to 6, the only legal final grades.</summary>
    public static bool IsLegalFinalGrade(decimal value) =>
        value is >= 1m and <= 6m && value == decimal.Truncate(value);

    /// <summary>
    /// Throws <see cref="InvariantViolationException"/> if <paramref name="value"/> is not one of the legal final
    /// grades in <see cref="FinalGradeValues"/>.
    /// </summary>
    public static void ValidateFinalGrade(decimal value)
    {
        if (!IsLegalFinalGrade(value))
        {
            throw new InvariantViolationException(
                $"{value} is not a legal final grade. Legal final grades are: {string.Join(", ", FinalGradeValues)}.");
        }
    }

    /// <summary>
    /// Parses typed input such as "3" or " 3 " into a final grade. The input is parsed as a grade token first
    /// (see <see cref="TryParseToken"/>), and is accepted only when it is a whole grade, so "2-" is rejected.
    /// </summary>
    /// <param name="input">The raw typed text.</param>
    /// <param name="value">The final grade when parsing succeeds, otherwise 0.</param>
    /// <returns><see langword="true"/> when <paramref name="input"/> is a legal final grade.</returns>
    public static bool TryParseFinalGradeToken(string? input, out decimal value)
    {
        if (TryParseToken(input, out value) && IsLegalFinalGrade(value))
        {
            return true;
        }

        value = 0m;
        return false;
    }

    /// <summary>
    /// Proposes the final grade for a subject average: the average as displayed (rounded by
    /// <see cref="AverageCalculator.RoundForDisplay"/>), rounded to a whole grade with .5 going to the worse
    /// grade (2.50 gives 3, 2.49 gives 2), and kept within 1 to 6. Rounding the displayed value, not the raw
    /// one, keeps the proposal consistent with the number the teacher sees.
    /// </summary>
    /// <param name="subjectAverage">The weighted subject average, or <see langword="null"/> when there is none.</param>
    /// <returns>The proposed final grade, or <see langword="null"/> when there is no average.</returns>
    public static decimal? ProposeFinalGrade(decimal? subjectAverage)
    {
        if (subjectAverage is not { } average)
        {
            return null;
        }

        // Averages are positive and a higher value is worse, so a midpoint rounded away from zero goes to the worse grade.
        var whole = Math.Round(AverageCalculator.RoundForDisplay(average), 0, MidpointRounding.AwayFromZero);
        return Math.Clamp(whole, FinalGradeValues[0], FinalGradeValues[^1]);
    }
}
