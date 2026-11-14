namespace NexGrades.Domain.Grading;

/// <summary>One grade bucket of a subject in the per-student grade list, with the student's grades in it.</summary>
/// <param name="BucketId">The bucket id.</param>
/// <param name="Name">The bucket name.</param>
/// <param name="Weight">The snapshotted bucket weight, never the live template weight.</param>
/// <param name="Assessments">The bucket's active assessments, ordered by date and then id.</param>
/// <param name="Average">The student's bucket average, or null when there is none (no counting grade, or exempt).</param>
/// <param name="Grades">The student's grades in the bucket, ordered by date and then id.</param>
public sealed record StudentBucketGrades(
    int BucketId,
    string Name,
    decimal Weight,
    IReadOnlyList<GradeSheetAssessment> Assessments,
    decimal? Average,
    IReadOnlyList<StudentGradeEntry> Grades);
