namespace NexGrades.Domain.Grading;

/// <summary>One grade as the grade sheet shows it.</summary>
/// <param name="Id">The grade id.</param>
/// <param name="GradeBucketId">The bucket the grade counts toward.</param>
/// <param name="Value">The numeric grade value; lower is better.</param>
/// <param name="Token">The display token for <paramref name="Value"/> (for example "2-"), or the invariant-culture value when legacy data is off the scale.</param>
/// <param name="RecordedOn">The date the grade applies to.</param>
/// <param name="Label">The optional label.</param>
/// <param name="Comment">The optional comment.</param>
/// <param name="VoidedOn">
/// When the grade stopped counting, or null while it counts. It is the grade's own voided time, or that of its
/// assessment when the assessment is voided, so a non-null value always means "excluded from the averages".
/// </param>
public sealed record GradeSheetGrade(
    int Id,
    int GradeBucketId,
    decimal Value,
    string Token,
    DateOnly RecordedOn,
    string? Label,
    string? Comment,
    DateTime? VoidedOn);
