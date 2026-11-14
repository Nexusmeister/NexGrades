namespace NexGrades.Domain.Grading;

/// <summary>Whether a grade sheet cell accepts a grade for its student and assessment.</summary>
public enum GradeSheetCellState
{
    /// <summary>The student was enrolled on the assessment date and takes part in the subject.</summary>
    Editable,

    /// <summary>The student was not enrolled on the assessment date (not yet joined, or already left).</summary>
    NotEnrolled,

    /// <summary>The student is exempt from the subject; exempt wins over any grade the student already has.</summary>
    Exempt,
}
