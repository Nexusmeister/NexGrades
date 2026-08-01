namespace NexGrades.Data.Entities;

public enum ClassInstanceStatus
{
    Active,
    RolledForward,
    Archived,
}

/// <summary>
/// One cohort in one school year — where teaching actually happens. <see cref="Name"/> is per-instance,
/// so a rename (e.g. 5A -> 6A) is a new row, never an update of a historic one.
/// </summary>
public class ClassInstance
{
    public int Id { get; set; }

    public int ClassGroupId { get; set; }

    public ClassGroup ClassGroup { get; set; } = null!;

    public int SchoolYearId { get; set; }

    public SchoolYear SchoolYear { get; set; } = null!;

    /// <summary>E.g. "5A". Per-instance — never mutated on rename.</summary>
    public required string Name { get; set; }

    /// <summary>Nullable: covers "teacher no longer has this class" without deleting anything.</summary>
    public int? TeacherId { get; set; }

    public Teacher? Teacher { get; set; }

    public ClassInstanceStatus Status { get; set; } = ClassInstanceStatus.Active;

    public ICollection<Enrollment> Enrollments { get; set; } = [];

    public ICollection<SubjectInstance> SubjectInstances { get; set; } = [];

    public ICollection<Note> Notes { get; set; } = [];
}
