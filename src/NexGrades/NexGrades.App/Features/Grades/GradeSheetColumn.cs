using System.Globalization;

namespace NexGrades.App.Features.Grades;

/// <summary>
/// Describes one grid column of the grade sheet. The columns are rebuilt from a list of these because a
/// <c>DataGrid</c> cannot bind its <c>Columns</c>. Cells are bound by index, so the index properties tell the
/// behavior which element of <see cref="GradeSheetRow.Cells"/> or <see cref="GradeSheetRow.Buckets"/> to show.
/// </summary>
public sealed class GradeSheetColumn
{
    /// <summary>What the column shows.</summary>
    public required GradeSheetColumnKind Kind { get; init; }

    /// <summary>The header text; for an assessment its title.</summary>
    public required string Header { get; init; }

    /// <summary>The header's second line: an assessment's date, formatted for the current culture. Empty otherwise.</summary>
    public string SubHeader { get; init; } = string.Empty;

    /// <summary>The name of the bucket the column belongs to; empty for the student and subject columns.</summary>
    public string BucketName { get; init; } = string.Empty;

    /// <summary>The id of the bucket the column belongs to, if any.</summary>
    public int? BucketId { get; init; }

    /// <summary>For an assessment column the assessment's id.</summary>
    public int? AssessmentId { get; init; }

    /// <summary>For an assessment column its title; used to prefill the edit form.</summary>
    public string AssessmentTitle { get; init; } = string.Empty;

    /// <summary>For an assessment column its date; used to prefill the edit form.</summary>
    public DateOnly? AssessmentDate { get; init; }

    /// <summary>True for an assessment column whose assessment is voided (shown only with "show voided").</summary>
    public bool IsVoided { get; init; }

    /// <summary>For an assessment column the index into <see cref="GradeSheetRow.Cells"/>; otherwise -1.</summary>
    public int CellIndex { get; init; } = -1;

    /// <summary>For a bucket column the index into <see cref="GradeSheetRow.Buckets"/>; otherwise -1.</summary>
    public int BucketIndex { get; init; } = -1;

    /// <summary>
    /// The binding path, relative to a <see cref="GradeSheetRow"/>, of the text this column shows. Editable only
    /// for assessment columns.
    /// </summary>
    public string BindingPath => Kind switch
    {
        GradeSheetColumnKind.Student => nameof(GradeSheetRow.StudentName),
        GradeSheetColumnKind.Assessment => CellPath(nameof(GradeCellViewModel.Text)),
        GradeSheetColumnKind.OtherGrades => string.Create(CultureInfo.InvariantCulture, $"{nameof(GradeSheetRow.Buckets)}[{BucketIndex}].{nameof(GradeSheetBucketCell.OtherGradesText)}"),
        GradeSheetColumnKind.BucketAverage => string.Create(CultureInfo.InvariantCulture, $"{nameof(GradeSheetRow.Buckets)}[{BucketIndex}].{nameof(GradeSheetBucketCell.AverageText)}"),
        _ => nameof(GradeSheetRow.SubjectAverageText),
    };

    /// <summary>
    /// The binding path, relative to a <see cref="GradeSheetRow"/>, of a property of this assessment column's
    /// <see cref="GradeCellViewModel"/>.
    /// </summary>
    /// <param name="propertyName">The cell property, for example <c>VisualState</c>.</param>
    /// <returns>The path.</returns>
    public string CellPath(string propertyName) =>
        string.Create(CultureInfo.InvariantCulture, $"{nameof(GradeSheetRow.Cells)}[{CellIndex}].{propertyName}");

    /// <summary>True when the user may type into the column's cells (subject to each cell's own state).</summary>
    public bool IsEditable => Kind == GradeSheetColumnKind.Assessment;
}
