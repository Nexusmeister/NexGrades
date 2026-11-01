namespace NexGrades.Data.Entities;

/// <summary>
/// A copy, taken from <see cref="BucketTemplate"/> when the owning <see cref="SubjectInstance"/> was created,
/// then independently editable. Deliberately carries no FK back to the template it was copied from — that
/// independence is the entire point of the snapshot: editing a template later must never reach backwards into
/// a past year and silently change a computed average.
/// </summary>
public class GradeBucket
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>The subject instance this bucket belongs to.</summary>
    public int SubjectInstanceId { get; set; }

    /// <summary>Navigation property for <see cref="SubjectInstanceId"/>.</summary>
    public SubjectInstance SubjectInstance { get; set; } = null!;

    /// <summary>Bucket label, e.g. "Schriftlich". Copied from the template, then editable on this row only.</summary>
    public required string Name { get; set; }

    /// <summary>Weight in the subject average. Averages use this snapshot, never the template's current weight.</summary>
    public decimal Weight { get; set; }

    /// <summary>Display order of the bucket within its subject instance.</summary>
    public int SortOrder { get; set; }

    /// <summary>The grades recorded in this bucket.</summary>
    public ICollection<Grade> Grades { get; set; } = [];
}
