namespace NexGrades.Data.Entities;

/// <summary>
/// One cohort in one school year — where teaching actually happens. <see cref="Name"/> is per-instance,
/// so a rename (e.g. 5A -> 6A) is a new row, never an update of a historic one.
/// </summary>
public class ClassInstance
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>The persistent cohort this instance is a yearly occurrence of.</summary>
    public int ClassGroupId { get; set; }

    /// <summary>Navigation property for <see cref="ClassGroupId"/>.</summary>
    public ClassGroup ClassGroup { get; set; } = null!;

    /// <summary>The school year this instance belongs to. Only SchoolYear, ClassInstance and Note carry a year column.</summary>
    public int SchoolYearId { get; set; }

    /// <summary>Navigation property for <see cref="SchoolYearId"/>.</summary>
    public SchoolYear SchoolYear { get; set; } = null!;

    /// <summary>E.g. "5A". Per-instance — never mutated on rename.</summary>
    public required string Name { get; set; }

    /// <summary>Nullable: covers "teacher no longer has this class" without deleting anything.</summary>
    public int? TeacherId { get; set; }

    /// <summary>Navigation property for <see cref="TeacherId"/>. Null while the class has no teacher.</summary>
    public Teacher? Teacher { get; set; }

    /// <summary>Lifecycle state. Rolled-forward and archived instances stay in the database as history.</summary>
    public ClassInstanceStatus Status { get; set; } = ClassInstanceStatus.Active;

    /// <summary>Student memberships of this class, including students who have since left.</summary>
    public ICollection<Enrollment> Enrollments { get; set; } = [];

    /// <summary>The subjects taught to this class in this school year.</summary>
    public ICollection<SubjectInstance> SubjectInstances { get; set; } = [];

    /// <summary>Notes that were written while this class was the student's class.</summary>
    public ICollection<Note> Notes { get; set; } = [];
}
