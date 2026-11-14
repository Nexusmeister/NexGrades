using NexGrades.App.Features.Grades;
using NexGrades.Domain.Grading;

namespace NexGrades.App.Tests;

public class GradeCellViewModelTests
{
    private static readonly DateTime VoidedAt = new(2025, 11, 1, 9, 0, 0);

    [Fact]
    public void IsEditable_ReturnsTrue_ForNormalEditableCell()
    {
        var cell = CreateCell();

        Assert.True(cell.IsEditable);
    }

    [Fact]
    public void IsEditable_ReturnsTrue_ForEditableCellWithActiveGrade()
    {
        var cell = CreateCell(grade: CreateGrade());

        Assert.True(cell.IsEditable);
    }

    [Fact]
    public void IsEditable_ReturnsFalse_ForVoidedAssessment()
    {
        var cell = CreateCell(assessmentVoided: true);

        Assert.False(cell.IsEditable);
    }

    [Fact]
    public void IsEditable_ReturnsFalse_ForVoidedGrade()
    {
        var cell = CreateCell(grade: CreateGrade(VoidedAt));

        Assert.False(cell.IsEditable);
    }

    [Fact]
    public void IsEditable_ReturnsFalse_WhenYearIsNotWritable()
    {
        var cell = CreateCell(isWritable: false);

        Assert.False(cell.IsEditable);
    }

    [Fact]
    public void IsEditable_ReturnsFalse_ForExemptCell()
    {
        var cell = CreateCell(state: GradeSheetCellState.Exempt);

        Assert.False(cell.IsEditable);
    }

    [Fact]
    public void IsEditable_ReturnsFalse_ForNotEnrolledCell()
    {
        var cell = CreateCell(state: GradeSheetCellState.NotEnrolled);

        Assert.False(cell.IsEditable);
    }

    [Fact]
    public void SetWritable_KeepsText_WhenYearBecomesLocked()
    {
        var cell = CreateCell();
        cell.SetTextWithoutCommit("3+");

        cell.SetWritable(false);

        Assert.Equal("3+", cell.Text);
        Assert.False(cell.IsEditable);
    }

    [Fact]
    public void SetWritable_EnablesEditing_WhenYearBecomesWritable()
    {
        var cell = CreateCell(isWritable: false);

        cell.SetWritable(true);

        Assert.True(cell.IsEditable);
    }

    [Fact]
    public void SetWritable_RaisesPropertyChangedForIsEditable_WhenWritabilityChanges()
    {
        var cell = CreateCell();
        var changed = new List<string?>();
        cell.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        cell.SetWritable(false);

        Assert.Contains(nameof(GradeCellViewModel.IsEditable), changed);
    }

    [Fact]
    public void RetrySaveIfFailed_DoesNothing_WhenNoErrorIsSet()
    {
        var commits = new List<GradeCellViewModel>();
        var cell = CreateCell(textCommitted: commits.Add);

        cell.RetrySaveIfFailed();

        Assert.Empty(commits);
    }

    [Fact]
    public void RetrySaveIfFailed_CommitsTheCellAgain_WhenErrorIsSet()
    {
        var commits = new List<GradeCellViewModel>();
        var cell = CreateCell(textCommitted: commits.Add);
        cell.HasError = true;

        cell.RetrySaveIfFailed();

        Assert.Same(cell, Assert.Single(commits));
    }

    private static GradeCellViewModel CreateCell(
        GradeSheetCellState state = GradeSheetCellState.Editable,
        GradeSheetGrade? grade = null,
        bool assessmentVoided = false,
        bool isWritable = true,
        Action<GradeCellViewModel>? textCommitted = null) =>
        new(
            1,
            new GradeSheetCell(AssessmentId: 10, State: state, Grade: grade),
            assessmentVoided,
            isWritable,
            textCommitted ?? (_ => { }));

    private static GradeSheetGrade CreateGrade(DateTime? voidedOn = null) =>
        new(
            Id: 100,
            GradeBucketId: 20,
            Value: 2.3m,
            Token: "2-",
            RecordedOn: new DateOnly(2025, 10, 1),
            Label: null,
            Comment: null,
            VoidedOn: voidedOn);
}
