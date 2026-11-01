namespace NexGrades.Domain.Export;

/// <summary>
/// One cell of a <see cref="SubjectOverviewTable"/>: text, a number, or empty. An empty cell means "no value", for
/// example a bucket without grades has no average; it never stands for zero.
/// </summary>
/// <param name="Text">The text content, or <see langword="null"/>.</param>
/// <param name="Number">The numeric content (kept as <see cref="decimal"/> for exactness), or <see langword="null"/>.</param>
public readonly record struct SubjectOverviewCell(string? Text, decimal? Number)
{
    /// <summary>A cell without a value.</summary>
    public static SubjectOverviewCell Empty => default;

    /// <summary>Creates a text cell.</summary>
    public static SubjectOverviewCell FromText(string text) => new(text, null);

    /// <summary>Creates a numeric cell, or an empty cell when <paramref name="number"/> is <see langword="null"/>.</summary>
    public static SubjectOverviewCell FromNumber(decimal? number) => number is null ? Empty : new(null, number);
}
