namespace NexGrades.App.Features.Classes;

/// <summary>A subject taught to the class, as listed on the class page with its export action.</summary>
/// <param name="SubjectInstanceId">The subject instance's id.</param>
/// <param name="SubjectName">The subject's name.</param>
public sealed record SubjectInstanceRow(int SubjectInstanceId, string SubjectName);
