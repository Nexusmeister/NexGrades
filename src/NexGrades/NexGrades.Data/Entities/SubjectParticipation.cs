namespace NexGrades.Data.Entities;

public enum ParticipationMode
{
    Participating,
    Exempt,
}

/// <summary>
/// The per-student override on top of the class curriculum. Default participation follows class enrollment;
/// a row exists only where a student deviates (elective chosen, religion/ethics split, exemption), keeping
/// the common case free of rows.
/// </summary>
public class SubjectParticipation
{
    public int Id { get; set; }

    public int SubjectInstanceId { get; set; }

    public SubjectInstance SubjectInstance { get; set; } = null!;

    public int StudentId { get; set; }

    public Student Student { get; set; } = null!;

    public ParticipationMode Mode { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}
