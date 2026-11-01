namespace NexGrades.App.Features.Students;

/// <summary>One student of the students overview.</summary>
/// <param name="Id">The student id.</param>
/// <param name="FirstName">The first name.</param>
/// <param name="LastName">The last name.</param>
/// <param name="ClassName">The name of the student's class in the active school year, or empty when the student is not placed in one.</param>
public sealed record StudentRow(int Id, string FirstName, string LastName, string ClassName);
