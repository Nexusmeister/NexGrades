namespace NexGrades.App.Features.Students;

/// <summary>A school year selectable on the notes tab. Its lock state is tracked by the page's <c>YearLock</c>.</summary>
/// <param name="Id">The school year id.</param>
/// <param name="Label">The year's display label, e.g. "2025/26".</param>
public sealed record NoteYearOption(int Id, string Label);
