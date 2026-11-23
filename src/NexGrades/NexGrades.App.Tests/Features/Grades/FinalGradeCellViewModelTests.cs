using NexGrades.App.Features.Grades;

namespace NexGrades.App.Tests.Features.Grades;

public class FinalGradeCellViewModelTests
{
    [Fact]
    public void Text_ShowsOverrideAsWholeGrade_WhenOverrideExists()
    {
        var cell = CreateCell(finalGradeOverride: 3m);

        Assert.Equal("3", cell.Text);
        Assert.True(cell.HasOverride);
    }

    [Fact]
    public void IsEditable_ReturnsFalse_ForExemptStudent()
    {
        Assert.False(CreateCell(isExempt: true).IsEditable);
    }

    [Fact]
    public void IsEditable_ReturnsFalse_WhenYearIsNotWritable()
    {
        Assert.False(CreateCell(isWritable: false).IsEditable);
    }

    [Fact]
    public void VisualState_IsExempt_ForExemptStudent()
    {
        Assert.Equal(GradeCellVisualState.Exempt, CreateCell(isExempt: true).VisualState);
    }

    [Fact]
    public void VisualState_IsError_WhenHasError()
    {
        var cell = CreateCell(finalGradeOverride: 3m);

        cell.HasError = true;

        Assert.Equal(GradeCellVisualState.Error, cell.VisualState);
    }

    [Fact]
    public void VisualState_IsNormal_WithoutOverride()
    {
        Assert.Equal(GradeCellVisualState.Normal, CreateCell().VisualState);
    }

    [Fact]
    public void HasUnsavedText_ReturnsFalse_ForFreshCell()
    {
        Assert.False(CreateCell(finalGradeOverride: 3m).HasUnsavedText);
    }

    [Fact]
    public void HasUnsavedText_ReturnsTrue_WhenTextDiffersFromOverride()
    {
        var cell = CreateCell(finalGradeOverride: 3m);

        cell.SetTextWithoutCommit("4");

        Assert.True(cell.HasUnsavedText);
    }

    [Fact]
    public void ApplyOverride_ReplacesText_WhenNoNewerTextWasTyped()
    {
        var cell = CreateCell();
        cell.SetTextWithoutCommit(" 3 ");
        cell.HasError = true;

        cell.ApplyOverride(3m, "3");

        Assert.Equal("3", cell.Text);
        Assert.False(cell.HasError);
        Assert.Equal(GradeCellVisualState.Override, cell.VisualState);
    }

    [Fact]
    public void ApplyOverride_KeepsNewerTypedText_WhenTextChangedDuringSave()
    {
        var cell = CreateCell();
        cell.SetTextWithoutCommit("4");

        cell.ApplyOverride(3m, "3");

        Assert.Equal("4", cell.Text);
        Assert.Equal(3m, cell.Override);
    }

    [Fact]
    public void ApplyOverride_ClearsText_WhenOverrideRemoved()
    {
        var cell = CreateCell(finalGradeOverride: 3m);
        cell.SetTextWithoutCommit(string.Empty);

        cell.ApplyOverride(null, string.Empty);

        Assert.Equal(string.Empty, cell.Text);
        Assert.False(cell.HasOverride);
    }

    [Fact]
    public void Text_RaisesCommit_OnlyWhenUserChangesIt()
    {
        var commits = 0;
        var cell = CreateCell(textCommitted: _ => commits++);

        cell.Text = "3";
        cell.SetTextWithoutCommit("4");

        Assert.Equal(1, commits);
    }

    [Fact]
    public void RetrySaveIfFailed_Commits_OnlyWhenCellHasError()
    {
        var commits = 0;
        var cell = CreateCell(textCommitted: _ => commits++);

        cell.RetrySaveIfFailed();
        Assert.Equal(0, commits);

        cell.HasError = true;
        cell.RetrySaveIfFailed();
        Assert.Equal(1, commits);
    }

    [Fact]
    public void SetWritable_KeepsText()
    {
        var cell = CreateCell();
        cell.SetTextWithoutCommit("3");

        cell.SetWritable(false);

        Assert.Equal("3", cell.Text);
    }

    private static FinalGradeCellViewModel CreateCell(
        bool isExempt = false,
        decimal? finalGradeOverride = null,
        decimal? proposal = 2m,
        bool isWritable = true,
        Action<FinalGradeCellViewModel>? textCommitted = null) =>
        new(1, isExempt, finalGradeOverride, proposal, isWritable, textCommitted ?? (_ => { }));
}
