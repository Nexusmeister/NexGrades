using NexGrades.Data.Entities;

namespace NexGrades.Domain.Services;

/// <summary>
/// A read-only projection of a <see cref="Note"/> with the school year and class names already resolved, so
/// callers (the student page, a later export) don't need a database context to display it.
/// </summary>
/// <param name="Id">The note id.</param>
/// <param name="StudentId">The student the note is about.</param>
/// <param name="SchoolYearId">The school year the note belongs to.</param>
/// <param name="SchoolYearLabel">The label of that school year, e.g. "2025/26".</param>
/// <param name="ClassInstanceId">The class the student was in when the note was written, if recorded.</param>
/// <param name="ClassInstanceName">The name of that class, if recorded.</param>
/// <param name="Category">An optional free-text category, or <see langword="null"/>.</param>
/// <param name="Text">The note text.</param>
/// <param name="CreatedOn">When the note was created.</param>
/// <param name="LastEditedOn">When the note was last edited, or <see langword="null"/> if never.</param>
/// <param name="ArchivedOn">When the note was archived, or <see langword="null"/> if it is active.</param>
public sealed record NoteDto(
    int Id,
    int StudentId,
    int SchoolYearId,
    string SchoolYearLabel,
    int? ClassInstanceId,
    string? ClassInstanceName,
    string? Category,
    string Text,
    DateTime CreatedOn,
    DateTime? LastEditedOn,
    DateTime? ArchivedOn)
{
    /// <summary>True when the note has been archived.</summary>
    public bool IsArchived => ArchivedOn is not null;
}
