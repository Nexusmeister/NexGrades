using System.Globalization;
using NexGrades.App.Localization;
using NexGrades.Domain.Grading;

namespace NexGrades.App.Features.Grades;

/// <summary>
/// Turns a <see cref="GradeSheet"/> into the column list of the grid. Pure, so it is unit tested without WPF.
/// The order is: student, then per bucket its assessments, an "other grades" column only when some student has a
/// grade in the bucket that belongs to no assessment, and the bucket average; the subject average, the proposed final grade and the editable final grade come last.
/// </summary>
public static class GradeSheetColumnBuilder
{
    /// <summary>Builds the columns for <paramref name="sheet"/>.</summary>
    /// <param name="sheet">The loaded sheet.</param>
    /// <returns>The columns in display order.</returns>
    public static IReadOnlyList<GradeSheetColumn> Build(GradeSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var culture = CultureInfo.CurrentCulture;
        var columns = new List<GradeSheetColumn>
        {
            new() { Kind = GradeSheetColumnKind.Student, Header = TextResources.Text("Grades_Column_Student") },
        };

        var cellIndex = 0;
        for (var bucketIndex = 0; bucketIndex < sheet.Buckets.Count; bucketIndex++)
        {
            var bucket = sheet.Buckets[bucketIndex];
            foreach (var assessment in bucket.Assessments)
            {
                columns.Add(new GradeSheetColumn
                {
                    Kind = GradeSheetColumnKind.Assessment,
                    Header = assessment.Title,
                    SubHeader = assessment.Date.ToString("d", culture),
                    BucketName = bucket.Name,
                    BucketId = bucket.Id,
                    AssessmentId = assessment.Id,
                    AssessmentTitle = assessment.Title,
                    AssessmentDate = assessment.Date,
                    IsVoided = assessment.VoidedOn is not null,
                    CellIndex = cellIndex++,
                    BucketIndex = bucketIndex,
                });
            }

            if (sheet.Students.Any(s => s.UnassignedGrades.Any(g => g.GradeBucketId == bucket.Id)))
            {
                columns.Add(new GradeSheetColumn
                {
                    Kind = GradeSheetColumnKind.OtherGrades,
                    Header = TextResources.Format("Grades_Column_OtherGrades_Format", bucket.Name),
                    BucketName = bucket.Name,
                    BucketId = bucket.Id,
                    BucketIndex = bucketIndex,
                });
            }

            columns.Add(new GradeSheetColumn
            {
                Kind = GradeSheetColumnKind.BucketAverage,
                Header = TextResources.Format("Grades_Column_CategoryAverage_Format", bucket.Name),
                BucketName = bucket.Name,
                BucketId = bucket.Id,
                BucketIndex = bucketIndex,
            });
        }

        columns.Add(new GradeSheetColumn { Kind = GradeSheetColumnKind.SubjectAverage, Header = TextResources.Text("Grades_Column_Average") });
        columns.Add(new GradeSheetColumn { Kind = GradeSheetColumnKind.ProposedFinalGrade, Header = TextResources.Text("Grades_Column_Proposal") });
        columns.Add(new GradeSheetColumn { Kind = GradeSheetColumnKind.FinalGrade, Header = TextResources.Text("Grades_Column_FinalGrade") });
        return columns;
    }
}
