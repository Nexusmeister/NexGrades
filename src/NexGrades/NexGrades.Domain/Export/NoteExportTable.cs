namespace NexGrades.Domain.Export;

/// <summary>
/// A library-free table of one student's notes in a date range. A writer (for example Excel) turns it into a file.
/// </summary>
/// <param name="FirstName">The student's first name.</param>
/// <param name="LastName">The student's last name.</param>
/// <param name="From">First day of the range, by the notes' creation date.</param>
/// <param name="To">Last day of the range; the whole day is included.</param>
/// <param name="IncludeArchived">Whether archived notes are part of <paramref name="Rows"/>.</param>
/// <param name="ExportedOn">When the export was built, from <c>IClock</c>.</param>
/// <param name="Rows">The notes, oldest first. Empty when nothing matches the range.</param>
public sealed record NoteExportTable(
    string FirstName,
    string LastName,
    DateOnly From,
    DateOnly To,
    bool IncludeArchived,
    DateTime ExportedOn,
    IReadOnlyList<NoteExportRow> Rows)
{
    /// <summary>The number of notes in the export.</summary>
    public int NoteCount => Rows.Count;
}
