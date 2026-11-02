namespace NexGrades.App.Features.SchoolYears;

/// <summary>What the teacher decided for one class at the end of the year.</summary>
public enum ClassYearEndChoice
{
    /// <summary>No decision yet.</summary>
    None,

    /// <summary>Carry the class into the next school year under a new name.</summary>
    RollForward,

    /// <summary>Archive the class; it gets no successor.</summary>
    Archive,
}
