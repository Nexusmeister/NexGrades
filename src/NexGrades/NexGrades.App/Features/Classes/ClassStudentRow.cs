namespace NexGrades.App.Features.Classes;

/// <summary>A student on a class roster; <paramref name="Period"/> is pre-formatted for the current UI culture.</summary>
/// <param name="StudentId">The student's id.</param>
/// <param name="FullName">The name as shown in the roster, e.g. "Last, First".</param>
/// <param name="Period">The enrollment period as text, for example a join date or a join and leave date.</param>
public sealed record ClassStudentRow(int StudentId, string FullName, string Period);
