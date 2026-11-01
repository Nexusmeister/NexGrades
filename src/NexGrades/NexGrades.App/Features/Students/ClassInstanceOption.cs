namespace NexGrades.App.Features.Students;

/// <summary>A class of the active school year that a student can be added to.</summary>
/// <param name="Id">The class instance id.</param>
/// <param name="Name">The class name in this school year.</param>
public sealed record ClassInstanceOption(int Id, string Name);
