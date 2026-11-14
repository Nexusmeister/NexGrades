namespace NexGrades.App.Features.Grades;

/// <summary>The look of a grade cell; the page styles each value, and must not rely on colour alone.</summary>
public enum GradeCellVisualState
{
    /// <summary>An ordinary editable cell, with or without a grade.</summary>
    Normal,

    /// <summary>The typed text is not a legal grade or could not be saved.</summary>
    Error,

    /// <summary>The student was not enrolled in the class on the assessment's date.</summary>
    NotEnrolled,

    /// <summary>The student is exempt from the subject.</summary>
    Exempt,

    /// <summary>The cell's assessment is voided.</summary>
    AssessmentVoided,

    /// <summary>The cell shows a voided grade that can be restored.</summary>
    Voided,
}
