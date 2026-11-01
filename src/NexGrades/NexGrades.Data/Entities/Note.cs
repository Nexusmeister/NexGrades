namespace NexGrades.Data.Entities;

/// <summary>
/// A free-standing note, independent of grades. Deliberately hangs off <see cref="Student"/> +
/// <see cref="Entities.SchoolYear"/> rather than off a class, so it survives the student changing class
/// mid-year; <see cref="ClassInstanceId"/> records the context it was written in (copied from the student's
/// enrollment in that year when the note is created).
/// </summary>
/// <remarks>
/// <see cref="StudentId"/> and <see cref="SchoolYearId"/> never change after creation: the closed-year
/// interceptor resolves a note's year from <see cref="SchoolYearId"/>, so moving a note to another year or
/// student would bypass the lock of the year it came from. Notes are never deleted; see
/// <see cref="ArchivedOn"/>.
/// </remarks>
public class Note
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>The student the note is about. Fixed at creation; see the remarks.</summary>
    public int StudentId { get; set; }

    /// <summary>Navigation property for <see cref="StudentId"/>.</summary>
    public Student Student { get; set; } = null!;

    /// <summary>The school year the note belongs to. Fixed at creation, because the closed-year lock is resolved from it.</summary>
    public int SchoolYearId { get; set; }

    /// <summary>Navigation property for <see cref="SchoolYearId"/>.</summary>
    public SchoolYear SchoolYear { get; set; } = null!;

    /// <summary>The class the note was written in, or null. Copied from the student's enrollment in that year at creation.</summary>
    public int? ClassInstanceId { get; set; }

    /// <summary>Navigation property for <see cref="ClassInstanceId"/>.</summary>
    public ClassInstance? ClassInstance { get; set; }

    /// <summary>Optional short label for grouping notes. Trimmed and length-limited by <c>NoteService</c>.</summary>
    public string? Category { get; set; }

    /// <summary>The note body. Required; <c>NoteService</c> rejects empty or over-long text.</summary>
    public required string Text { get; set; }

    /// <summary>When the note was created.</summary>
    public DateTime CreatedOn { get; set; }

    /// <summary>When the text or category was last changed; null if the note has never been edited.</summary>
    public DateTime? LastEditedOn { get; set; }

    /// <summary>
    /// When the note was archived, or <see langword="null"/> while it is active. Set by archiving and cleared
    /// by restoring; this is how a note is "removed", because nothing is ever deleted.
    /// </summary>
    public DateTime? ArchivedOn { get; set; }
}
