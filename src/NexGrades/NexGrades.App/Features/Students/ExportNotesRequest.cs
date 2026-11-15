namespace NexGrades.App.Features.Students;

/// <summary>What the teacher chose in the export-notes dialog.</summary>
/// <param name="From">First day of the range.</param>
/// <param name="To">Last day of the range; the whole day is included.</param>
/// <param name="IncludeArchived">Whether archived notes are exported too.</param>
public sealed record ExportNotesRequest(DateOnly From, DateOnly To, bool IncludeArchived);
