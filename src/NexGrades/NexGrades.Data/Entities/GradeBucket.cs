namespace NexGrades.Data.Entities;

/// <summary>
/// A copy, taken from <see cref="BucketTemplate"/> when the owning <see cref="SubjectInstance"/> was created,
/// then independently editable. Deliberately carries no FK back to the template it was copied from — that
/// independence is the entire point of the snapshot: editing a template later must never reach backwards into
/// a past year and silently change a computed average.
/// </summary>
public class GradeBucket
{
    public int Id { get; set; }

    public int SubjectInstanceId { get; set; }

    public SubjectInstance SubjectInstance { get; set; } = null!;

    public required string Name { get; set; }

    public decimal Weight { get; set; }

    public int SortOrder { get; set; }

    public ICollection<Grade> Grades { get; set; } = [];
}
