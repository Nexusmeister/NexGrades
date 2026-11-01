namespace NexGrades.Data.Entities;

/// <summary>Lifecycle of a school year: <c>Planned</c>, then <c>Active</c>, then <c>Closed</c>.</summary>
public enum SchoolYearStatus
{
    /// <summary>Created but not the current year yet.</summary>
    Planned,

    /// <summary>The current year. At most one year is active at a time.</summary>
    Active,

    /// <summary>Read-only unless temporarily unlocked through <see cref="SchoolYear.UnlockedUntil"/>.</summary>
    Closed,
}
