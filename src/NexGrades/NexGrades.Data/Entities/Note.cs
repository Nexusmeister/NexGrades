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
    public int Id { get; set; }

    public int StudentId { get; set; }

    public Student Student { get; set; } = null!;

    public int SchoolYearId { get; set; }

    public SchoolYear SchoolYear { get; set; } = null!;

    public int? ClassInstanceId { get; set; }

    public ClassInstance? ClassInstance { get; set; }

    public string? Category { get; set; }

    public required string Text { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime? LastEditedOn { get; set; }

    /// <summary>
    /// When the note was archived, or <see langword="null"/> while it is active. Set by archiving and cleared
    /// by restoring; this is how a note is "removed", because nothing is ever deleted.
    /// </summary>
    public DateTime? ArchivedOn { get; set; }
}
