namespace NexGrades.Data.Entities;

/// <summary>
/// A single grade. The school year is implicit via Grade -> GradeBucket -> SubjectInstance ->
/// ClassInstance -> SchoolYear; there is deliberately no year column here to get out of sync.
/// </summary>
public class Grade
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>The bucket this grade counts toward.</summary>
    public int GradeBucketId { get; set; }

    /// <summary>Navigation property for <see cref="GradeBucketId"/>.</summary>
    public GradeBucket GradeBucket { get; set; } = null!;

    /// <summary>The student who received the grade.</summary>
    public int StudentId { get; set; }

    /// <summary>Navigation property for <see cref="StudentId"/>.</summary>
    public Student Student { get; set; } = null!;

    /// <summary>
    /// The grade on the 1 to 6 scale with tendencies, stored as <c>decimal</c>, lower is better. Legal values are
    /// defined by <c>GradingScale</c> (for example, 1+ is 0.7).
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>The date the grade applies to. Enrollment is checked as of this date, not as of today.</summary>
    public DateOnly RecordedOn { get; set; }

    /// <summary>E.g. "Klassenarbeit 2".</summary>
    public string? Label { get; set; }

    /// <summary>Optional free-text remark about the grade.</summary>
    public string? Comment { get; set; }
}
