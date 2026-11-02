using NexGrades.Data.Entities;

namespace NexGrades.Domain.Services;

/// <summary>A school year as the school-year flow shows it, without the tracked entity.</summary>
/// <param name="Id">The school year id.</param>
/// <param name="Label">The display label, e.g. "2025/26".</param>
/// <param name="StartDate">First day of the school year.</param>
/// <param name="EndDate">Last day of the school year.</param>
/// <param name="Status">Where the year is in its Planned, Active, Closed lifecycle.</param>
public sealed record SchoolYearSummary(int Id, string Label, DateTime StartDate, DateTime EndDate, SchoolYearStatus Status);
