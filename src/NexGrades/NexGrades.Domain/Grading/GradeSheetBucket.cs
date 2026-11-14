namespace NexGrades.Domain.Grading;

/// <summary>A grade bucket on the grade sheet with its assessment columns.</summary>
/// <param name="Id">The bucket id.</param>
/// <param name="Name">The bucket name.</param>
/// <param name="Weight">The snapshotted bucket weight, never the live template weight.</param>
/// <param name="SortOrder">The bucket position among its siblings.</param>
/// <param name="Assessments">The bucket assessments, ordered by date and then id.</param>
public sealed record GradeSheetBucket(
    int Id,
    string Name,
    decimal Weight,
    int SortOrder,
    IReadOnlyList<GradeSheetAssessment> Assessments);
