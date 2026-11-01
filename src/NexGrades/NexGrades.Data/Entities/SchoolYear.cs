namespace NexGrades.Data.Entities;

/// <summary>
/// The root scoping concept for the whole model. At most one year is <see cref="SchoolYearStatus.Active"/>
/// at a time; <see cref="SchoolYearStatus.Closed"/> is what makes the data it scopes read-only.
/// </summary>
public class SchoolYear
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Human-readable label, e.g. "2025/26".</summary>
    public required string Label { get; set; }

    /// <summary>First day of the school year.</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Last day of the school year.</summary>
    public DateTime EndDate { get; set; }

    /// <summary>Lifecycle state. Only one year is <see cref="SchoolYearStatus.Active"/> at a time.</summary>
    public SchoolYearStatus Status { get; set; } = SchoolYearStatus.Planned;

    /// <summary>When the year was closed, or null while it is not closed.</summary>
    public DateTime? ClosedOn { get; set; }

    /// <summary>
    /// Until when a closed year is temporarily unlocked for writes, set by <c>UnlockService</c>. Null means locked;
    /// a future value unlocks the year until that time.
    /// </summary>
    public DateTime? UnlockedUntil { get; set; }

    /// <summary>The cohorts first formed in this school year.</summary>
    public ICollection<ClassGroup> FoundedClassGroups { get; set; } = [];

    /// <summary>The class instances of this school year.</summary>
    public ICollection<ClassInstance> ClassInstances { get; set; } = [];

    /// <summary>The notes that belong to this school year.</summary>
    public ICollection<Note> Notes { get; set; } = [];
}
