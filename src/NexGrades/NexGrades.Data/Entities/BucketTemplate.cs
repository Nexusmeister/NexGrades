namespace NexGrades.Data.Entities;

/// <summary>
/// The subject's default bucket layout. Copied into a <see cref="GradeBucket"/> snapshot whenever a
/// <see cref="SubjectInstance"/> is created; editing a template afterwards affects only subject instances
/// created afterwards.
/// </summary>
public class BucketTemplate
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>The subject that owns this template.</summary>
    public int SubjectId { get; set; }

    /// <summary>Navigation property for <see cref="SubjectId"/>.</summary>
    public Subject Subject { get; set; } = null!;

    /// <summary>E.g. "Schriftlich".</summary>
    public required string Name { get; set; }

    /// <summary>Weight copied into each new <see cref="GradeBucket"/> snapshot. Changing it does not affect existing snapshots.</summary>
    public decimal Weight { get; set; }

    /// <summary>Display order of the bucket within its subject.</summary>
    public int SortOrder { get; set; }
}
