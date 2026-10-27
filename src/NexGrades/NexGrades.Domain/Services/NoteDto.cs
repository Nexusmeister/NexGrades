using NexGrades.Data.Entities;

namespace NexGrades.Domain.Services;

/// <summary>
/// A read-only projection of a <see cref="Note"/> with the school year and class names already resolved, so
/// callers (the student page, a later export) don't need a database context to display it.
/// </summary>
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

/// <summary>
/// A school year a student has an enrollment in, together with whether notes may be written to it right now.
/// </summary>
/// <param name="IsWritable">
/// False for a closed year that is not unlocked; see <see cref="Auditing.SchoolYearLock.IsWritable"/>.
/// </param>
public sealed record NoteYear(
    int SchoolYearId,
    string Label,
    SchoolYearStatus Status,
    DateTime? UnlockedUntil,
    bool IsWritable);
