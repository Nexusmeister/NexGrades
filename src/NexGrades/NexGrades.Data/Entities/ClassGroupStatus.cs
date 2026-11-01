namespace NexGrades.Data.Entities;

/// <summary>
/// Whether a cohort is still taught. <see cref="Ended"/> is a teacher's decision made when archiving a class,
/// never a side effect of rolling a year forward.
/// </summary>
public enum ClassGroupStatus
{
    /// <summary>The cohort is still taught.</summary>
    Active,

    /// <summary>The cohort is over. Its history stays in the database.</summary>
    Ended,
}
