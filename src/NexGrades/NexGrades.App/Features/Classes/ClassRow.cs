namespace NexGrades.App.Features.Classes;

/// <summary>One class of the active school year, as listed on the classes overview.</summary>
/// <param name="Id">The class instance id.</param>
/// <param name="Name">The class name in this school year, e.g. "5A".</param>
/// <param name="CohortLabel">The cohort name shared across school years, taken from the class group.</param>
/// <param name="StudentCount">The number of students who are enrolled and have not left.</param>
public sealed record ClassRow(int Id, string Name, string CohortLabel, int StudentCount);
