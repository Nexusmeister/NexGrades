namespace NexGrades.App.Features.Students;

/// <summary>A school year selectable on the notes tab, with its lock state.</summary>
/// <param name="Id">The school year id.</param>
/// <param name="Label">The year's display label, e.g. "2025/26".</param>
/// <param name="IsWritable">Notes can currently be added or changed (the year is not closed, or is unlocked).</param>
/// <param name="IsClosed">The year is closed (whether or not it is currently unlocked).</param>
/// <param name="UnlockedUntil">When a temporary unlock of a closed year ends, if there is one.</param>
public sealed record NoteYearOption(int Id, string Label, bool IsWritable, bool IsClosed, DateTime? UnlockedUntil);
