namespace NexGrades.App.Features.SchoolYears;

/// <summary>
/// What another page asks the School year page to do when it navigates there. A visit without a request (the sidebar)
/// just shows the page as it was.
/// </summary>
public enum SchoolYearPageRequest
{
    /// <summary>Start the end-of-year flow, as if the teacher had pressed its start button on the overview.</summary>
    StartEndOfYearFlow,
}
