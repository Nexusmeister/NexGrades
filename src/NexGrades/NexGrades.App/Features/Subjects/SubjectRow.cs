namespace NexGrades.App.Features.Subjects;

/// <summary>One subject of the subject catalogue.</summary>
/// <param name="Id">The subject id.</param>
/// <param name="Name">The subject name, e.g. "Mathematik".</param>
/// <param name="ShortCode">The short code of the subject.</param>
public sealed record SubjectRow(int Id, string Name, string ShortCode);
