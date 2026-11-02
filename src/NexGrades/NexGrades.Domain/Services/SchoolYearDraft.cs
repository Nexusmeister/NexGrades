namespace NexGrades.Domain.Services;

/// <summary>The editable values of a school year that is about to be created.</summary>
/// <param name="Label">The display label, e.g. "2026/27".</param>
/// <param name="StartDate">First day of the school year.</param>
/// <param name="EndDate">Last day of the school year.</param>
public sealed record SchoolYearDraft(string Label, DateTime StartDate, DateTime EndDate);
