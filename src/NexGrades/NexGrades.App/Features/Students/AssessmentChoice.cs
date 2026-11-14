namespace NexGrades.App.Features.Students;

/// <summary>
/// One entry of the assessment picker in the grade editor: either "no assessment" (a single grade) or an active
/// assessment of the bucket.
/// </summary>
/// <param name="Id">The assessment id, or <see langword="null"/> for the "no assessment" entry.</param>
/// <param name="DisplayText">What the picker shows, formatted for the current UI culture.</param>
/// <param name="Date">The assessment's date, which an assessment grade takes; <see langword="null"/> for "no assessment".</param>
public sealed record AssessmentChoice(int? Id, string DisplayText, DateOnly? Date);
