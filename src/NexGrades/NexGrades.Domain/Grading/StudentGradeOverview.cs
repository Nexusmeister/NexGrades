namespace NexGrades.Domain.Grading;

/// <summary>
/// All grades of one student in one school year, per subject and bucket, with the averages. A student who is
/// not enrolled in that year has no subjects.
/// </summary>
/// <param name="StudentId">The student id.</param>
/// <param name="SchoolYearId">The school year id.</param>
/// <param name="SchoolYearLabel">The school year label, for example "2025/26".</param>
/// <param name="JoinedOn">When the student joined the class that year, or null when not enrolled.</param>
/// <param name="LeftOn">When the student left the class, or null when still enrolled or not enrolled.</param>
/// <param name="Subjects">The subjects of the student's class, ordered by name.</param>
public sealed record StudentGradeOverview(
    int StudentId,
    int SchoolYearId,
    string SchoolYearLabel,
    DateOnly? JoinedOn,
    DateOnly? LeftOn,
    IReadOnlyList<StudentSubjectGrades> Subjects);
