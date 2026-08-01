namespace NexGrades.Data.Entities;

/// <summary>
/// Catalogue entry / template, e.g. "Mathematik". Not year-scoped.
/// </summary>
public class Subject
{
    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>E.g. "MA".</summary>
    public required string ShortCode { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<BucketTemplate> BucketTemplates { get; set; } = [];

    public ICollection<SubjectInstance> SubjectInstances { get; set; } = [];
}
