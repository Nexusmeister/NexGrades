namespace NexGrades.Domain.Services;

/// <summary>
/// One finding of the close-readiness check. It is structured so the UI can word and link it itself; the ids
/// are for linking only and must never be displayed.
/// </summary>
/// <param name="Kind">What was found.</param>
/// <param name="ClassInstanceId">The class the finding belongs to.</param>
/// <param name="ClassName">The class name, for display.</param>
/// <param name="SubjectInstanceId">The subject instance, for subject and student findings.</param>
/// <param name="SubjectName">The subject name, for display.</param>
/// <param name="StudentId">The student, for <see cref="CloseReadinessIssueKind.StudentWithoutGrade"/>.</param>
/// <param name="StudentFirstName">The student's first name, for display.</param>
/// <param name="StudentLastName">The student's last name, for display.</param>
/// <param name="WeightTotal">The summed bucket weights, for <see cref="CloseReadinessIssueKind.BucketWeightsNotHundred"/>.</param>
public sealed record CloseReadinessIssue(
    CloseReadinessIssueKind Kind,
    int ClassInstanceId,
    string ClassName,
    int? SubjectInstanceId = null,
    string? SubjectName = null,
    int? StudentId = null,
    string? StudentFirstName = null,
    string? StudentLastName = null,
    decimal? WeightTotal = null);
