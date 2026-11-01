namespace NexGrades.App.Features.Classes;

/// <summary>A student on a class roster; <paramref name="Period"/> is pre-formatted for the current UI culture.</summary>
public sealed record ClassStudentRow(int StudentId, string FullName, string Period);
