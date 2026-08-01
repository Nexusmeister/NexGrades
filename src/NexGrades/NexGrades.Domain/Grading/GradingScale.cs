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
    private static readonly IReadOnlyDictionary<string, decimal> TokenToValueMap = new Dictionary<string, decimal>
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
    public static IReadOnlyCollection<decimal> LegalValues { get; } = ValueToTokenMap.Keys.ToList();

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
            : throw new ArgumentException($"'{token}' is not a legal grade token.", nameof(token));

    /// <summary>Converts a numeric grade value to its display token (e.g. "2-"). Throws for an illegal value.</summary>
    public static string TokenForValue(decimal value) =>
        ValueToTokenMap.TryGetValue(value, out var token)
            ? token
            : throw new ArgumentException($"{value} is not a legal grade value.", nameof(value));

    /// <summary>Non-throwing variant of <see cref="ValueForToken"/>.</summary>
    public static bool TryGetValueForToken(string token, out decimal value) => TokenToValueMap.TryGetValue(token, out value);

    /// <summary>Non-throwing variant of <see cref="TokenForValue"/>.</summary>
    public static bool TryGetTokenForValue(decimal value, out string? token) => ValueToTokenMap.TryGetValue(value, out token);
}
