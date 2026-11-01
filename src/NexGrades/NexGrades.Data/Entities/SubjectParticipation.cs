namespace NexGrades.Data.Entities;

/// <summary>
/// The per-student override on top of the class curriculum. Default participation follows class enrollment;
/// a row exists only where a student deviates (elective chosen, religion/ethics split, exemption), keeping
/// the common case free of rows.
/// </summary>
public class SubjectParticipation
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>The subject instance the override applies to.</summary>
    public int SubjectInstanceId { get; set; }

    /// <summary>Navigation property for <see cref="SubjectInstanceId"/>.</summary>
    public SubjectInstance SubjectInstance { get; set; } = null!;

    /// <summary>The student the override applies to.</summary>
    public int StudentId { get; set; }

    /// <summary>Navigation property for <see cref="StudentId"/>.</summary>
    public Student Student { get; set; } = null!;

    /// <summary>Whether the student takes part in, or is exempt from, the subject instance.</summary>
    public ParticipationMode Mode { get; set; }

    /// <summary>Start of the period the override applies to. Copied unchanged on rollover; no Domain rule checks it yet.</summary>
    public DateTime? From { get; set; }

    /// <summary>End of the period the override applies to. Copied unchanged on rollover; no Domain rule checks it yet.</summary>
    public DateTime? To { get; set; }
}
