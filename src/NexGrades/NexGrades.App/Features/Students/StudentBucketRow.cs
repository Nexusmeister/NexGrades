using NexGrades.App.Features.Grades;
using NexGrades.App.Localization;
using NexGrades.Domain.Grading;

namespace NexGrades.App.Features.Students;

/// <summary>One grade bucket of a subject in the student's grade list, with the student's grades in it.</summary>
public sealed class StudentBucketRow
{
    /// <summary>Creates the row from the loaded bucket.</summary>
    /// <param name="bucket">The bucket with its grades.</param>
    /// <param name="isExempt">Whether the student is exempt from the bucket's subject.</param>
    public StudentBucketRow(StudentBucketGrades bucket, bool isExempt)
    {
        ArgumentNullException.ThrowIfNull(bucket);

        BucketId = bucket.BucketId;
        Name = bucket.Name;
        IsExempt = isExempt;
        WeightText = TextResources.Format("Students_Grades_Weight_Format", bucket.Weight);
        AverageText = GradeAverageFormatter.Format(bucket.Average);
        Assessments = bucket.Assessments;
        Grades = [.. bucket.Grades.Select(g => new StudentGradeRow(g, isExempt))];
    }

    /// <summary>The bucket id.</summary>
    public int BucketId { get; }

    /// <summary>The bucket name.</summary>
    public string Name { get; }

    /// <summary>Whether the student is exempt from the subject; exempt students cannot get new grades.</summary>
    public bool IsExempt { get; }

    /// <summary>The snapshotted weight, formatted for the current culture.</summary>
    public string WeightText { get; }

    /// <summary>The bucket average, or an en dash when there is none.</summary>
    public string AverageText { get; }

    /// <summary>The bucket's active assessments, from which the editor offers those without a grade yet.</summary>
    public IReadOnlyList<GradeSheetAssessment> Assessments { get; }

    /// <summary>The student's grades in the bucket.</summary>
    public IReadOnlyList<StudentGradeRow> Grades { get; }

    /// <summary>True when the student has no grade shown in this bucket.</summary>
    public bool HasNoGrades => Grades.Count == 0;
}
