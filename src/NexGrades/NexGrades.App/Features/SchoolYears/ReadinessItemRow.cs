namespace NexGrades.App.Features.SchoolYears;

/// <summary>One line of the readiness step: the finding as text, and where to go to fix it.</summary>
/// <param name="Text">The finding, already formatted for the current UI culture.</param>
/// <param name="Target">The page the item links to.</param>
/// <param name="TargetId">The id handed to that page. It is for navigation only and is never shown.</param>
public sealed record ReadinessItemRow(string Text, ReadinessLinkTarget Target, int TargetId);
