namespace NexGrades.App.Features.SchoolYears;

/// <summary>The steps of the guided "End school year" flow, in order.</summary>
public enum EndYearStep
{
    /// <summary>Explains the flow and shows the active year.</summary>
    Overview,

    /// <summary>Creates the next school year as a Planned year.</summary>
    NextYear,

    /// <summary>Rolls forward or archives each class of the closing year.</summary>
    Classes,

    /// <summary>Shows what blocks the close and what only deserves a look.</summary>
    Readiness,

    /// <summary>Asks for the final confirmation.</summary>
    Confirm,

    /// <summary>Reports that the year was closed and the next one is active.</summary>
    Result,
}
