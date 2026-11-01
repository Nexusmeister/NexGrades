namespace NexGrades.Domain.Export;

/// <summary>One student's row of a <see cref="SubjectOverviewTable"/>.</summary>
/// <param name="Cells">The cells; <c>Cells[i]</c> belongs to <c>Columns[i]</c> of the table.</param>
/// <param name="LeftOn">
/// The date the student left the class, or <see langword="null"/> for an active student. The Domain does not
/// format it; the writer adds the "left on" marker in the UI culture.
/// </param>
public sealed record SubjectOverviewRow(IReadOnlyList<SubjectOverviewCell> Cells, DateOnly? LeftOn);
