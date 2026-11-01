namespace NexGrades.Domain.Export;

/// <summary>
/// A library-free, display-neutral table of every participating student's grades and averages in one subject
/// instance. A writer (for example Excel) turns it into a file.
/// </summary>
/// <param name="SubjectName">The subject's name.</param>
/// <param name="ClassName">The class instance's name.</param>
/// <param name="SchoolYearLabel">The school year's label, for example "2025/26".</param>
/// <param name="Columns">The columns, in order: student, then per bucket its grades and average, then the subject average.</param>
/// <param name="Rows">Active students first, then students who left but have grades, each group sorted by name.</param>
public sealed record SubjectOverviewTable(
    string SubjectName,
    string ClassName,
    string SchoolYearLabel,
    IReadOnlyList<SubjectOverviewColumn> Columns,
    IReadOnlyList<SubjectOverviewRow> Rows);
