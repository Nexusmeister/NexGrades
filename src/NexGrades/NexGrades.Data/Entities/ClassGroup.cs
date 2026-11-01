namespace NexGrades.Data.Entities;

/// <summary>
/// The persistent cohort. Survives renames across school years; carries no grades itself —
/// grades attach to the yearly <see cref="ClassInstance"/>.
/// </summary>
public class ClassGroup
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>E.g. "Kohorte 2019".</summary>
    public required string CohortLabel { get; set; }

    /// <summary>The school year in which the cohort was first formed. Never changes after that.</summary>
    public int FoundedInSchoolYearId { get; set; }

    /// <summary>Navigation property for <see cref="FoundedInSchoolYearId"/>.</summary>
    public SchoolYear FoundedInSchoolYear { get; set; } = null!;

    /// <summary>Whether the cohort is still taught. Set to <see cref="ClassGroupStatus.Ended"/> only by an explicit teacher decision.</summary>
    public ClassGroupStatus Status { get; set; } = ClassGroupStatus.Active;

    /// <summary>The yearly class instances of this cohort, one per school year.</summary>
    public ICollection<ClassInstance> ClassInstances { get; set; } = [];
}
