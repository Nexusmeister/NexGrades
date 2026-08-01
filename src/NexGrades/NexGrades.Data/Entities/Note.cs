namespace NexGrades.Data.Entities;

/// <summary>
/// A free-standing note, independent of grades. Deliberately hangs off <see cref="Student"/> +
/// <see cref="Entities.SchoolYear"/> rather than off a class, so it survives the student changing class
/// mid-year; <see cref="ClassInstanceId"/> records the context it was written in.
/// </summary>
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
}
