namespace NexGrades.Data.Entities;

/// <summary>
/// A subject taught to one class instance in one year.
/// </summary>
public class SubjectInstance
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>The class instance the subject is taught to.</summary>
    public int ClassInstanceId { get; set; }

    /// <summary>Navigation property for <see cref="ClassInstanceId"/>.</summary>
    public ClassInstance ClassInstance { get; set; } = null!;

    /// <summary>The subject taught. Each subject appears at most once per class instance (unique index).</summary>
    public int SubjectId { get; set; }

    /// <summary>Navigation property for <see cref="SubjectId"/>.</summary>
    public Subject Subject { get; set; } = null!;

    /// <summary>The teacher of this subject for this class, or null when unassigned.</summary>
    public int? TeacherId { get; set; }

    /// <summary>Navigation property for <see cref="TeacherId"/>. Null while the subject has no teacher.</summary>
    public Teacher? Teacher { get; set; }

    /// <summary>The snapshotted buckets that this subject's average is computed from.</summary>
    public ICollection<GradeBucket> GradeBuckets { get; set; } = [];

    /// <summary>Per-student overrides of participation in this subject instance.</summary>
    public ICollection<SubjectParticipation> SubjectParticipations { get; set; } = [];
}
