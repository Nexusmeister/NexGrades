namespace NexGrades.Data.Entities;

/// <summary>
/// Lifecycle of a class instance within its school year. Any state other than <see cref="Active"/> keeps the row
/// as history rather than removing it.
/// </summary>
public enum ClassInstanceStatus
{
    /// <summary>The class is in use for its school year.</summary>
    Active,

    /// <summary>The class was carried into the next school year, which received a new, independent instance.</summary>
    RolledForward,

    /// <summary>The class was closed without a successor instance.</summary>
    Archived,
}
