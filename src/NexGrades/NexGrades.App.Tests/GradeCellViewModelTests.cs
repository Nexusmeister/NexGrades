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

    [Fact]
    public void ApplyGrade_KeepsNewerText_WhenTextChangedDuringSave()
    {
        var cell = CreateCell(grade: CreateGrade());
        cell.HasError = true;
        cell.IsSaving = true;
        cell.Text = "2";
        var savedGrade = CreateGrade(token: "3", value: 3.0m);

        cell.ApplyGrade(savedGrade, savedText: "3");

        Assert.Equal("2", cell.Text);
        Assert.Same(savedGrade, cell.Grade);
        Assert.True(cell.HasActiveGrade);
        Assert.True(cell.HasUnsavedText);
        Assert.True(cell.HasError);
    }

    [Fact]
    public void ApplyGrade_ReplacesTextWithToken_WhenTextUnchanged()
    {
        var cell = CreateCell();
        cell.HasError = true;
        cell.Text = " 2- ";

        cell.ApplyGrade(CreateGrade(), savedText: "2-");

        Assert.Equal("2-", cell.Text);
        Assert.False(cell.HasError);
    }

    [Fact]
    public void ApplyGrade_KeepsNewerText_WhenClearedCellThenTypedDuringVoid()
    {
        var cell = CreateCell(grade: CreateGrade());
        cell.Text = "4";

        cell.ApplyGrade(null, savedText: string.Empty);

        Assert.Equal("4", cell.Text);
        Assert.False(cell.HasActiveGrade);
        Assert.True(cell.HasUnsavedText);
    }

    [Fact]
    public void HasUnsavedText_ReturnsFalse_WhenTextMatchesSavedToken()
    {
        var cell = CreateCell(grade: CreateGrade());

        Assert.False(cell.HasUnsavedText);
    }

    [Fact]
    public void HasUnsavedText_ReturnsTrue_WhenSaveIsQueued()
    {
        var cell = CreateCell(grade: CreateGrade());
        cell.SaveQueued = true;

        Assert.True(cell.HasUnsavedText);
    }

    [Fact]
    public void HasUnsavedText_ReturnsTrue_WhileSaving()
    {
        var cell = CreateCell(grade: CreateGrade());
        cell.IsSaving = true;

        Assert.True(cell.HasUnsavedText);
    }

    [Fact]
    public void ApplyGrade_OverwritesText_WhenCalledWithoutSavedText()
    {
        var cell = CreateCell();
        cell.HasError = true;
        cell.Text = "4";

        cell.ApplyGrade(CreateGrade());

        Assert.Equal("2-", cell.Text);
        Assert.False(cell.HasError);
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

    private static GradeSheetGrade CreateGrade(DateTime? voidedOn = null, string token = "2-", decimal value = 2.3m) =>
        new(
            Id: 100,
            GradeBucketId: 20,
            Value: value,
            Token: token,
            RecordedOn: new DateOnly(2025, 10, 1),
            Label: null,
            Comment: null,
            VoidedOn: voidedOn);
}
