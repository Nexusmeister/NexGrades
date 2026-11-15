namespace NexGrades.Domain.Export;

/// <summary>One student's row of a <see cref="SubjectOverviewTable"/>.</summary>
/// <param name="Cells">The cells; <c>Cells[i]</c> belongs to <c>Columns[i]</c> of the table.</param>
/// <param name="LeftOn">
/// The date the student left the class, or <see langword="null"/> for an active student. The Domain does not
/// format it; the writer adds the "left on" marker in the UI culture.
/// </param>
/// <param name="IsFinalGradeOverridden">
/// Whether the teacher set the final grade. A flag, not text, so the writer words it in the UI language; the cell of
/// the <see cref="SubjectOverviewColumnKind.FinalGradeOverridden"/> column is empty.
/// </param>
public sealed record SubjectOverviewRow(
    IReadOnlyList<SubjectOverviewCell> Cells, DateOnly? LeftOn, bool IsFinalGradeOverridden = false);
