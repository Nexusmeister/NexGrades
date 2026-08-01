namespace NexGrades.Data.Entities;

/// <summary>
/// A subject taught to one class instance in one year.
/// </summary>
public class SubjectInstance
{
    public int Id { get; set; }

    public int ClassInstanceId { get; set; }

    public ClassInstance ClassInstance { get; set; } = null!;

    public int SubjectId { get; set; }

    public Subject Subject { get; set; } = null!;

    public int? TeacherId { get; set; }

    public Teacher? Teacher { get; set; }

    public ICollection<GradeBucket> GradeBuckets { get; set; } = [];

    public ICollection<SubjectParticipation> SubjectParticipations { get; set; } = [];
}
