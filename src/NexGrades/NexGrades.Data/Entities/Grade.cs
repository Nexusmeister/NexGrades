namespace NexGrades.Data.Entities;

/// <summary>
/// A single grade. The school year is implicit via Grade -> GradeBucket -> SubjectInstance ->
/// ClassInstance -> SchoolYear; there is deliberately no year column here to get out of sync.
/// </summary>
/// <remarks>
/// A grade either belongs to an <see cref="Entities.Assessment"/> or stands alone (<see cref="AssessmentId"/> is
/// null). When it belongs to one, <c>Grade.GradeBucketId == Assessment.GradeBucketId</c> always holds: the bucket
/// is taken from the assessment when the grade is recorded, and neither side's bucket changes afterwards. The
/// schema cannot express this, so the Domain keeps it. Grades are never deleted; see <see cref="VoidedOn"/>.
/// </remarks>
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

    /// <summary>
    /// The assessment this grade was given for, or null for a single grade with no assessment. Its bucket is
    /// always <see cref="GradeBucketId"/>; see the remarks. A partial unique index allows at most one active grade
    /// per student and assessment.
    /// </summary>
    public int? AssessmentId { get; set; }

    /// <summary>Navigation property for <see cref="AssessmentId"/>. Null for a grade with no assessment.</summary>
    public Assessment? Assessment { get; set; }

    /// <summary>
    /// When the grade was voided, or <see langword="null"/> while it is active. Set by voiding and cleared by
    /// restoring; this is how a grade is "removed", because nothing is ever deleted. A voided grade counts toward
    /// no average.
    /// </summary>
    public DateTime? VoidedOn { get; set; }
}
