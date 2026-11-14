namespace NexGrades.Domain.Grading;

/// <summary>One assessment column on the grade sheet.</summary>
/// <param name="Id">The assessment id.</param>
/// <param name="Title">The display title.</param>
/// <param name="Date">The date the assessment took place.</param>
/// <param name="VoidedOn">When the assessment was voided; always null unless voided items are shown.</param>
public sealed record GradeSheetAssessment(int Id, string Title, DateOnly Date, DateTime? VoidedOn);
