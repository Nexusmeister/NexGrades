namespace NexGrades.Data.Entities;

/// <summary>
/// Catalogue entry / template, e.g. "Mathematik". Not year-scoped.
/// </summary>
public class Subject
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Display name, e.g. "Mathematik".</summary>
    public required string Name { get; set; }

    /// <summary>E.g. "MA".</summary>
    public required string ShortCode { get; set; }

    /// <summary>Whether the subject is currently offered. Stored only; no Domain or App code reads it yet.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The default bucket layout, copied into each new subject instance.</summary>
    public ICollection<BucketTemplate> BucketTemplates { get; set; } = [];

    /// <summary>Every place this subject is taught, across classes and school years.</summary>
    public ICollection<SubjectInstance> SubjectInstances { get; set; } = [];
}
