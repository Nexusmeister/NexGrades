namespace NexGrades.Data.Entities;

public enum SchoolYearStatus
{
    Planned,
    Active,
    Closed,
}

/// <summary>
/// The root scoping concept for the whole model. At most one year is <see cref="SchoolYearStatus.Active"/>
/// at a time; <see cref="SchoolYearStatus.Closed"/> is what makes the data it scopes read-only.
/// </summary>
public class SchoolYear
{
    public int Id { get; set; }

    /// <summary>Human-readable label, e.g. "2025/26".</summary>
    public required string Label { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public SchoolYearStatus Status { get; set; } = SchoolYearStatus.Planned;

    public DateTime? ClosedOn { get; set; }

    public DateTime? UnlockedUntil { get; set; }

    public ICollection<ClassGroup> FoundedClassGroups { get; set; } = [];

    public ICollection<ClassInstance> ClassInstances { get; set; } = [];

    public ICollection<Note> Notes { get; set; } = [];
}
