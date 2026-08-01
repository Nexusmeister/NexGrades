namespace NexGrades.Data.Entities;

/// <summary>
/// A single grade. The school year is implicit via Grade -> GradeBucket -> SubjectInstance ->
/// ClassInstance -> SchoolYear; there is deliberately no year column here to get out of sync.
/// </summary>
public class Grade
{
    public int Id { get; set; }

    public int GradeBucketId { get; set; }

    public GradeBucket GradeBucket { get; set; } = null!;

    public int StudentId { get; set; }

    public Student Student { get; set; } = null!;

    public decimal Value { get; set; }

    public DateOnly RecordedOn { get; set; }

    /// <summary>E.g. "Klassenarbeit 2".</summary>
    public string? Label { get; set; }

    public string? Comment { get; set; }
}
