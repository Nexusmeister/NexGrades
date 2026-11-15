using System.Globalization;
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
        FinalGradeText = GradeAverageFormatter.FormatFinalGrade(subject.FinalGrade);
        HasFinalGradeOverride = subject.FinalGradeOverride is not null;
        FinalGradeDescription = DescribeFinalGrade(subject);
        Buckets = [.. subject.Buckets.Select(b => new StudentBucketRow(b, subject.IsExempt))];
    }

    /// <summary>The subject name.</summary>
    public string SubjectName { get; }

    /// <summary>Whether the student is exempt from the subject: no averages, and no grades can be added.</summary>
    public bool IsExempt { get; }

    /// <summary>The weighted subject average, or an en dash when there is none.</summary>
    public string SubjectAverageText { get; }

    /// <summary>The final grade as a whole number, or empty when there is none or the student is exempt.</summary>
    public string FinalGradeText { get; }

    /// <summary>True when the final grade is an override set by the teacher.</summary>
    public bool HasFinalGradeOverride { get; }

    /// <summary>A whole-sentence description of the final grade; empty when there is none or the student is exempt.</summary>
    public string FinalGradeDescription { get; }

    /// <summary>The subject's buckets.</summary>
    public IReadOnlyList<StudentBucketRow> Buckets { get; }

    private static string DescribeFinalGrade(StudentSubjectGrades subject)
    {
        if (subject.FinalGrade is null)
        {
            return string.Empty;
        }

        var finalGrade = GradeAverageFormatter.FormatFinalGrade(subject.FinalGrade);
        if (subject.FinalGradeOverride is null)
        {
            return string.Format(CultureInfo.CurrentCulture, "Final grade: {0}", finalGrade);
        }

        return subject.ProposedFinalGrade is null
            ? string.Format(CultureInfo.CurrentCulture, "Final grade: {0} (set by teacher, no proposal)", finalGrade)
            : string.Format(
                CultureInfo.CurrentCulture,
                "Final grade: {0} (set by teacher, proposal {1})",
                finalGrade,
                GradeAverageFormatter.FormatFinalGrade(subject.ProposedFinalGrade));
    }
}
