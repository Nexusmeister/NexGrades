namespace NexGrades.Domain.Grading;

/// <summary>One student row on the grade sheet.</summary>
/// <param name="StudentId">The student id.</param>
/// <param name="FirstName">The first name.</param>
/// <param name="LastName">The last name.</param>
/// <param name="JoinedOn">The first day of the student enrollment in the class.</param>
/// <param name="LeftOn">The day the student left the class, or null while still enrolled. Such students are shown, marked, so earlier grades can still be entered.</param>
/// <param name="IsExempt">Whether the student is exempt from the subject. The row is then read-only and has no averages.</param>
/// <param name="Cells">One cell per assessment, in column order: the buckets in order, each with its assessments in order.</param>
/// <param name="UnassignedGrades">Grades with no assessment (single grades); their <see cref="GradeSheetGrade.GradeBucketId"/> tells the bucket.</param>
/// <param name="BucketAverages">The average per bucket id; null for a bucket without grades.</param>
/// <param name="SubjectAverage">The weighted subject average, or null when no bucket has grades.</param>
public sealed record GradeSheetStudent(
    int StudentId,
    string FirstName,
    string LastName,
    DateOnly JoinedOn,
    DateOnly? LeftOn,
    bool IsExempt,
    IReadOnlyList<GradeSheetCell> Cells,
    IReadOnlyList<GradeSheetGrade> UnassignedGrades,
    IReadOnlyDictionary<int, decimal?> BucketAverages,
    decimal? SubjectAverage);
