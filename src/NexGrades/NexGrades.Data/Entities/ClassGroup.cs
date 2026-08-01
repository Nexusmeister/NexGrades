namespace NexGrades.Data.Entities;

public enum ClassGroupStatus
{
    Active,
    Ended,
}

/// <summary>
/// The persistent cohort. Survives renames across school years; carries no grades itself —
/// grades attach to the yearly <see cref="ClassInstance"/>.
/// </summary>
public class ClassGroup
{
    public int Id { get; set; }

    /// <summary>E.g. "Kohorte 2019".</summary>
    public required string CohortLabel { get; set; }

    public int FoundedInSchoolYearId { get; set; }

    public SchoolYear FoundedInSchoolYear { get; set; } = null!;

    public ClassGroupStatus Status { get; set; } = ClassGroupStatus.Active;

    public ICollection<ClassInstance> ClassInstances { get; set; } = [];
}
