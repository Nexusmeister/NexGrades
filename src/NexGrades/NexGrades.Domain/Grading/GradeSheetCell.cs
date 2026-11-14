namespace NexGrades.Domain.Grading;

/// <summary>The intersection of one student and one assessment on the grade sheet.</summary>
/// <param name="AssessmentId">The assessment (column) the cell belongs to.</param>
/// <param name="State">Whether the cell accepts a grade.</param>
/// <param name="Grade">
/// The student's active grade for the assessment, or, when voided grades are shown and there is no active
/// one, the latest voided grade; null when the student has none.
/// </param>
public sealed record GradeSheetCell(int AssessmentId, GradeSheetCellState State, GradeSheetGrade? Grade);
