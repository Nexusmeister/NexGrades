namespace NexGrades.Domain.Export;

/// <summary>One note in a <see cref="NoteExportTable"/>.</summary>
/// <param name="CreatedOn">When the note was created.</param>
/// <param name="SchoolYearLabel">The label of the school year the note belongs to, for example "2025/26".</param>
/// <param name="ClassName">The name of the class the note records, or <see langword="null"/> when it records none.</param>
/// <param name="Category">The note's category, or <see langword="null"/> when it has none.</param>
/// <param name="Text">The note body.</param>
/// <param name="LastEditedOn">When the note was last edited, or <see langword="null"/> if it never was.</param>
/// <param name="ArchivedOn">When the note was archived, or <see langword="null"/> while it is active.</param>
public sealed record NoteExportRow(
    DateTime CreatedOn,
    string SchoolYearLabel,
    string? ClassName,
    string? Category,
    string Text,
    DateTime? LastEditedOn,
    DateTime? ArchivedOn);
