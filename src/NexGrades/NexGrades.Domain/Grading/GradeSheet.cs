namespace NexGrades.Domain.Grading;

/// <summary>
/// Everything the grade sheet page needs for one subject instance: students as rows, assessments as columns
/// grouped by bucket, and the live averages.
/// </summary>
/// <param name="SubjectInstanceId">The subject instance the sheet shows.</param>
/// <param name="SubjectName">The subject name.</param>
/// <param name="ClassName">The class name.</param>
/// <param name="SchoolYearId">The school year the class belongs to.</param>
/// <param name="SchoolYearLabel">The school year label, e.g. "2025/26".</param>
/// <param name="IsWritable">Whether the year can be written to right now; false for a closed, locked year.</param>
/// <param name="IsClosed">Whether the year is closed, whether or not it is unlocked.</param>
/// <param name="UnlockedUntil">When the temporary unlock ends, or null when the year is not unlocked.</param>
/// <param name="Buckets">The buckets in display order.</param>
/// <param name="Students">Students still in the class first, then those who left, each by last and first name.</param>
public sealed record GradeSheet(
    int SubjectInstanceId,
    string SubjectName,
    string ClassName,
    int SchoolYearId,
    string SchoolYearLabel,
    bool IsWritable,
    bool IsClosed,
    DateTime? UnlockedUntil,
    IReadOnlyList<GradeSheetBucket> Buckets,
    IReadOnlyList<GradeSheetStudent> Students);
