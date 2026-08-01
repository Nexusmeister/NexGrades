namespace NexGrades.Data.Entities;

/// <summary>
/// A teacher. In practice there is one row for the app's owner (<see cref="IsCurrentUser"/> = true);
/// modelling it as an entity keeps a future handover a data change, not a schema migration.
/// </summary>
public class Teacher
{
    public int Id { get; set; }

    public required string DisplayName { get; set; }

    public bool IsCurrentUser { get; set; }

    public ICollection<ClassInstance> ClassInstances { get; set; } = [];

    public ICollection<SubjectInstance> SubjectInstances { get; set; } = [];
}
