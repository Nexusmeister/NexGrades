using NexGrades.App.Features.Grades;
using NexGrades.Domain.Grading;

namespace NexGrades.App.Features.Students;

/// <summary>
/// One subject in the student's grade list. Numbers are formatted once, with the current UI culture, so the view
/// does not depend on XAML string formats.
/// </summary>
public sealed class StudentSubjectRow
{
    /// <summary>Creates the row from the loaded subject.</summary>
    /// <param name="subject">The subject with its buckets and grades.</param>
    public StudentSubjectRow(StudentSubjectGrades subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        SubjectName = subject.SubjectName;
        IsExempt = subject.IsExempt;
        SubjectAverageText = GradeAverageFormatter.Format(subject.SubjectAverage);
        Buckets = [.. subject.Buckets.Select(b => new StudentBucketRow(b, subject.IsExempt))];
    }

    /// <summary>The subject name.</summary>
    public string SubjectName { get; }

    /// <summary>Whether the student is exempt from the subject: no averages, and no grades can be added.</summary>
    public bool IsExempt { get; }

    /// <summary>The weighted subject average, or an en dash when there is none.</summary>
    public string SubjectAverageText { get; }

    /// <summary>The subject's buckets.</summary>
    public IReadOnlyList<StudentBucketRow> Buckets { get; }
}
