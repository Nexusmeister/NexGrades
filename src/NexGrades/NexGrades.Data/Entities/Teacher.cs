namespace NexGrades.Data.Entities;

/// <summary>
/// A teacher. In practice there is one row for the app's owner (<see cref="IsCurrentUser"/> = true);
/// modelling it as an entity keeps a future handover a data change, not a schema migration.
/// </summary>
public class Teacher
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>The teacher's display name.</summary>
    public required string DisplayName { get; set; }

    /// <summary>True for the row that belongs to the app's owner.</summary>
    public bool IsCurrentUser { get; set; }

    /// <summary>The classes this teacher is assigned to, across school years.</summary>
    public ICollection<ClassInstance> ClassInstances { get; set; } = [];

    /// <summary>The subjects this teacher teaches, across school years.</summary>
    public ICollection<SubjectInstance> SubjectInstances { get; set; } = [];
}
