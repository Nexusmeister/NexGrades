using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using NexGrades.Domain.Grading;

namespace NexGrades.App.Features.Grades;

/// <summary>
/// One student row of the grade sheet. It owns the cells (one per assessment, in column order), the per-bucket
/// values and the subject average, and recomputes the averages from the cells after each save, using the same
/// <see cref="GradeSheetAverages"/> as the loader. An exempt student has no averages, as in the export.
/// </summary>
public sealed partial class GradeSheetRow : ObservableObject
{
    private readonly IReadOnlyList<GradeSheetBucket> _buckets;
    private readonly IReadOnlyList<GradeSheetGrade> _unassigned;

    /// <summary>Creates the row from a loaded student.</summary>
    /// <param name="student">The loaded student.</param>
    /// <param name="buckets">The sheet's buckets, in column order.</param>
    /// <param name="isWritable">Whether the school year accepts writes right now.</param>
    /// <param name="textCommitted">Called when a cell's text is committed; the owner saves it.</param>
    /// <param name="finalGradeCommitted">Called when the final grade cell's text is committed; the owner saves it.</param>
    public GradeSheetRow(
        GradeSheetStudent student,
        IReadOnlyList<GradeSheetBucket> buckets,
        bool isWritable,
        Action<GradeCellViewModel> textCommitted,
        Action<FinalGradeCellViewModel> finalGradeCommitted)
    {
        ArgumentNullException.ThrowIfNull(student);
        ArgumentNullException.ThrowIfNull(buckets);

        _buckets = buckets;
        _unassigned = student.UnassignedGrades;
        StudentId = student.StudentId;
        IsExempt = student.IsExempt;
        HasLeft = student.LeftOn is not null;

        var culture = CultureInfo.CurrentCulture;
        StudentName = string.Format(culture, "{0}, {1}", student.LastName, student.FirstName);
        StatusText = student.IsExempt
            ? "Exempt from this subject."
            : student.LeftOn is { } left
                ? string.Format(culture, "Left the class on {0:d}.", left)
                : string.Empty;

        var assessments = buckets.SelectMany(b => b.Assessments).ToDictionary(a => a.Id);
        Cells = [.. student.Cells.Select(c => new GradeCellViewModel(
            student.StudentId, c, assessments[c.AssessmentId].VoidedOn is not null, isWritable, textCommitted))];
        Buckets = [.. buckets.Select(_ => new GradeSheetBucketCell())];
        FinalGrade = new FinalGradeCellViewModel(
            student.StudentId, student.IsExempt, student.FinalGradeOverride, student.ProposedFinalGrade, isWritable, finalGradeCommitted);

        for (var i = 0; i < buckets.Count; i++)
        {
            Buckets[i].OtherGradesText = FormatOtherGrades(buckets[i].Id, culture);
        }

        RecomputeAverages();
    }

    /// <summary>The student's id.</summary>
    public int StudentId { get; }

    /// <summary>The name for the frozen column, as "Last, First".</summary>
    public string StudentName { get; }

    /// <summary>True when the student is exempt from the subject: the row is shown disabled, without averages.</summary>
    public bool IsExempt { get; }

    /// <summary>True when the student has left the class; the row stays so earlier grades can be entered.</summary>
    public bool HasLeft { get; }

    /// <summary>A whole-sentence note for the row ("Exempt ...", "Left the class on ..."); empty for a normal row.</summary>
    public string StatusText { get; }

    /// <summary>False for an exempt student, whose row is disabled.</summary>
    public bool IsRowEnabled => !IsExempt;

    /// <summary>The cells, one per assessment column in column order.</summary>
    public IReadOnlyList<GradeCellViewModel> Cells { get; }

    /// <summary>The per-bucket values, in bucket order.</summary>
    public IReadOnlyList<GradeSheetBucketCell> Buckets { get; }

    /// <summary>The final grade cell: the teacher's override of the proposal.</summary>
    public FinalGradeCellViewModel FinalGrade { get; }

    /// <summary>The proposed final grade as a whole grade; an en dash when there is none.</summary>
    [ObservableProperty]
    private string _proposalText = GradeAverageFormatter.Missing;

    /// <summary>The subject average formatted for display; an en dash when there is none.</summary>
    [ObservableProperty]
    private string _subjectAverageText = GradeAverageFormatter.Missing;

    /// <summary>Recomputes this row's bucket and subject averages from the grades currently in its cells.</summary>
    public void RecomputeAverages()
    {
        if (IsExempt)
        {
            foreach (var bucket in Buckets)
            {
                bucket.AverageText = GradeAverageFormatter.Missing;
            }

            SubjectAverageText = GradeAverageFormatter.Missing;
            ProposalText = GradeAverageFormatter.Missing;
            FinalGrade.SetProposal(null);
            return;
        }

        var grades = Cells.Where(c => c.Grade is not null).Select(c => c.Grade!).Concat(_unassigned);
        var averages = GradeSheetAverages.Compute(_buckets, grades);
        for (var i = 0; i < _buckets.Count; i++)
        {
            Buckets[i].AverageText = GradeAverageFormatter.Format(averages.BucketAverages[_buckets[i].Id]);
        }

        SubjectAverageText = GradeAverageFormatter.Format(averages.SubjectAverage);

        var proposal = GradingScale.ProposeFinalGrade(averages.SubjectAverage);
        ProposalText = proposal is null ? GradeAverageFormatter.Missing : FinalGradeCellViewModel.FormatGrade(proposal);
        FinalGrade.SetProposal(proposal);
    }

    /// <summary>Applies the year's writability to every cell, keeping typed text.</summary>
    /// <param name="isWritable">The year's current writability.</param>
    public void SetWritable(bool isWritable)
    {
        foreach (var cell in Cells)
        {
            cell.SetWritable(isWritable);
        }

        FinalGrade.SetWritable(isWritable);
    }

    /// <summary>Gets the editable cell of this row behind <paramref name="column"/>.</summary>
    /// <param name="column">The grid column.</param>
    /// <returns>The assessment cell or the final grade cell, or null for a column that is not editable.</returns>
    internal IGradeSheetEditableCell? EditableCell(GradeSheetColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);

        return column.Kind switch
        {
            GradeSheetColumnKind.FinalGrade => FinalGrade,
            GradeSheetColumnKind.Assessment when column.CellIndex >= 0 && column.CellIndex < Cells.Count => Cells[column.CellIndex],
            _ => null,
        };
    }

    private string FormatOtherGrades(int bucketId, CultureInfo culture) =>
        string.Join(
            culture.TextInfo.ListSeparator + " ",
            _unassigned.Where(g => g.GradeBucketId == bucketId)
                .Select(g => g.VoidedOn is null ? g.Token : string.Format(culture, "({0})", g.Token)));
}
