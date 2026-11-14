namespace NexGrades.Domain.Grading;

/// <summary>One grade of a student as the per-student grade list shows it.</summary>
/// <param name="Id">The grade id.</param>
/// <param name="GradeBucketId">The bucket the grade counts toward.</param>
/// <param name="AssessmentId">The assessment the grade belongs to, or null for a single grade.</param>
/// <param name="AssessmentTitle">The title of the assessment, or null for a single grade.</param>
/// <param name="Value">The numeric grade value; lower is better.</param>
/// <param name="Token">The display token for <paramref name="Value"/> (for example "2-"), or the invariant-culture value when legacy data is off the scale.</param>
/// <param name="RecordedOn">The date the grade applies to.</param>
/// <param name="Label">The optional label.</param>
/// <param name="Comment">The optional comment.</param>
/// <param name="VoidedOn">The grade's own voided time, or null when the grade itself is not voided.</param>
/// <param name="IsAssessmentVoided">Whether the grade's assessment is voided, which also excludes the grade.</param>
public sealed record StudentGradeEntry(
    int Id,
    int GradeBucketId,
    int? AssessmentId,
    string? AssessmentTitle,
    decimal Value,
    string Token,
    DateOnly RecordedOn,
    string? Label,
    string? Comment,
    DateTime? VoidedOn,
    bool IsAssessmentVoided)
{
    /// <summary>True when the grade counts toward the averages: neither it nor its assessment is voided.</summary>
    public bool IsCounting => VoidedOn is null && !IsAssessmentVoided;
}
