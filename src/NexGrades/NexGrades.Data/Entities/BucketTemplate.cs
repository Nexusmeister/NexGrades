namespace NexGrades.Data.Entities;

/// <summary>
/// The subject's default bucket layout. Copied into a <see cref="GradeBucket"/> snapshot whenever a
/// <see cref="SubjectInstance"/> is created; editing a template afterwards affects only subject instances
/// created afterwards.
/// </summary>
public class BucketTemplate
{
    public int Id { get; set; }

    public int SubjectId { get; set; }

    public Subject Subject { get; set; } = null!;

    /// <summary>E.g. "Schriftlich".</summary>
    public required string Name { get; set; }

    public decimal Weight { get; set; }

    public int SortOrder { get; set; }
}
