namespace NexGrades.Domain.Services;

/// <summary>
/// Result of <see cref="YearCloseService.GetCloseReadinessAsync"/>: blocking issues that must be resolved
/// before the year can close, and advisory warnings that are surfaced to the teacher but never block the
/// close.
/// </summary>
public sealed record CloseReadiness(IReadOnlyList<string> BlockingIssues, IReadOnlyList<string> Warnings)
{
    /// <summary>Whether <see cref="YearCloseService.CloseSchoolYearAsync"/> would succeed right now.</summary>
    public bool CanClose => BlockingIssues.Count == 0;
}
