using System.Globalization;
using NexGrades.App.Features.Grades;
using NexGrades.Domain.Grading;

namespace NexGrades.App.Tests;

public class GradeSheetRowTests
{
    private static readonly DateOnly SeptemberFirst = new(2025, 9, 3);

    [Fact]
    public void RecomputeAverages_ShowsEnDashForSubject_ForExemptRow()
    {
        var student = CreateStudent(
            [CreateCell(1, GradeSheetCellState.Exempt, CreateGrade(10, 2m))],
            isExempt: true);
        var row = CreateRow(student, [CreateBucket(10, 1m, CreateAssessment(1))]);

        InEnUs(() => row.RecomputeAverages());

        Assert.Equal(GradeAverageFormatter.Missing, row.SubjectAverageText);
    }

    [Fact]
    public void RecomputeAverages_ShowsEnDashForBucket_ForExemptRow()
    {
        var student = CreateStudent(
            [CreateCell(1, GradeSheetCellState.Exempt, CreateGrade(10, 2m))],
            isExempt: true);
        var row = CreateRow(student, [CreateBucket(10, 1m, CreateAssessment(1))]);

        InEnUs(() => row.RecomputeAverages());

        Assert.Equal(GradeAverageFormatter.Missing, row.Buckets[0].AverageText);
    }

    [Fact]
    public void RecomputeAverages_UpdatesSubjectAverage_AfterCellGradeChanges()
    {
        var student = CreateStudent(
            [
                CreateCell(1, GradeSheetCellState.Editable, CreateGrade(10, 2m)),
                CreateCell(2, GradeSheetCellState.Editable),
            ]);
        var row = CreateRow(student, [CreateBucket(10, 1m, CreateAssessment(1), CreateAssessment(2))]);

        row.Cells[1].ApplyGrade(CreateGrade(10, 4m));
        InEnUs(() => row.RecomputeAverages());

        Assert.Equal("3.00", row.SubjectAverageText);
    }

    [Fact]
    public void RecomputeAverages_IgnoresVoidedUnassignedGrade_InBucketAverage()
    {
        var student = CreateStudent(
            [],
            unassigned: [CreateGrade(10, 2m), CreateGrade(10, 5m, voidedOn: new DateTime(2025, 11, 1))]);
        var row = CreateRow(student, [CreateBucket(10, 1m)]);

        InEnUs(() => row.RecomputeAverages());

        Assert.Equal("2.00", row.Buckets[0].AverageText);
    }

    [Fact]
    public void RecomputeAverages_IgnoresVoidedUnassignedGrade_InSubjectAverage()
    {
        var student = CreateStudent(
            [],
            unassigned: [CreateGrade(10, 2m), CreateGrade(10, 5m, voidedOn: new DateTime(2025, 11, 1))]);
        var row = CreateRow(student, [CreateBucket(10, 1m)]);

        InEnUs(() => row.RecomputeAverages());

        Assert.Equal("2.00", row.SubjectAverageText);
    }

    [Fact]
    public void RecomputeAverages_ShowsEnDashForSubject_WhenOnlyVoidedUnassignedGradeExists()
    {
        var student = CreateStudent(
            [],
            unassigned: [CreateGrade(10, 5m, voidedOn: new DateTime(2025, 11, 1))]);
        var row = CreateRow(student, [CreateBucket(10, 1m)]);

        InEnUs(() => row.RecomputeAverages());

        Assert.Equal(GradeAverageFormatter.Missing, row.SubjectAverageText);
    }

    [Fact]
    public void RecomputeAverages_SetsProposalText_FromSubjectAverage()
    {
        var student = CreateStudent([CreateCell(1, GradeSheetCellState.Editable, CreateGrade(10, 2m))]);
        var row = CreateRow(student, [CreateBucket(10, 1m, CreateAssessment(1))]);

        InEnUs(() => row.RecomputeAverages());

        Assert.Equal("2", row.ProposalText);
        Assert.Equal(2m, row.FinalGrade.Proposal);
    }

    [Fact]
    public void RecomputeAverages_FollowsChangedGrade_ForProposalText()
    {
        var student = CreateStudent(
            [CreateCell(1, GradeSheetCellState.Editable, CreateGrade(10, 2m)), CreateCell(2, GradeSheetCellState.Editable)]);
        var row = CreateRow(student, [CreateBucket(10, 1m, CreateAssessment(1), CreateAssessment(2))]);

        row.Cells[1].ApplyGrade(CreateGrade(10, 5m));
        InEnUs(() => row.RecomputeAverages());

        Assert.Equal("4", row.ProposalText);
    }

    [Fact]
    public void RecomputeAverages_ShowsEnDashForProposal_WithoutGrades()
    {
        var row = CreateRow(CreateStudent([]), [CreateBucket(10, 1m)]);

        InEnUs(() => row.RecomputeAverages());

        Assert.Equal(GradeAverageFormatter.Missing, row.ProposalText);
        Assert.Null(row.FinalGrade.Proposal);
    }

    [Fact]
    public void RecomputeAverages_ShowsEnDashForProposal_ForExemptRow()
    {
        var student = CreateStudent([CreateCell(1, GradeSheetCellState.Exempt, CreateGrade(10, 2m))], isExempt: true);
        var row = CreateRow(student, [CreateBucket(10, 1m, CreateAssessment(1))]);

        InEnUs(() => row.RecomputeAverages());

        Assert.Equal(GradeAverageFormatter.Missing, row.ProposalText);
        Assert.False(row.FinalGrade.IsEditable);
    }

    [Fact]
    public void FinalGrade_HasOverrideVisualState_WhenStudentHasOverride()
    {
        var student = CreateStudent([]) with { FinalGradeOverride = 3m };
        var row = CreateRow(student, [CreateBucket(10, 1m)]);

        Assert.Equal(GradeCellVisualState.Override, row.FinalGrade.VisualState);
        Assert.Equal("3", row.FinalGrade.Text);
    }

    [Fact]
    public void SetWritable_KeepsTypedFinalGradeText_WhenYearBecomesReadOnly()
    {
        var row = CreateRow(CreateStudent([]), [CreateBucket(10, 1m)]);
        row.FinalGrade.SetTextWithoutCommit("3");

        row.SetWritable(false);

        Assert.Equal("3", row.FinalGrade.Text);
        Assert.False(row.FinalGrade.IsEditable);
    }

    [Fact]
    public void EditableCell_ReturnsFinalGradeCell_ForFinalGradeColumn()
    {
        var row = CreateRow(CreateStudent([]), [CreateBucket(10, 1m)]);
        var column = new GradeSheetColumn { Kind = GradeSheetColumnKind.FinalGrade, Header = "Final grade" };

        Assert.Same(row.FinalGrade, row.EditableCell(column));
    }

    [Fact]
    public void EditableCell_ReturnsNull_ForNonEditableColumn()
    {
        var row = CreateRow(CreateStudent([]), [CreateBucket(10, 1m)]);
        var column = new GradeSheetColumn { Kind = GradeSheetColumnKind.SubjectAverage, Header = "Average" };

        Assert.Null(row.EditableCell(column));
    }

    private static GradeSheetRow CreateRow(GradeSheetStudent student, IReadOnlyList<GradeSheetBucket> buckets) =>
        InEnUs(() => new GradeSheetRow(student, buckets, isWritable: true, textCommitted: _ => { }, finalGradeCommitted: _ => { }));

    private static GradeSheetStudent CreateStudent(
        IReadOnlyList<GradeSheetCell> cells,
        IReadOnlyList<GradeSheetGrade>? unassigned = null,
        bool isExempt = false) =>
        new(
            StudentId: 1,
            FirstName: "Ada",
            LastName: "Lovelace",
            JoinedOn: new DateOnly(2025, 8, 1),
            LeftOn: null,
            IsExempt: isExempt,
            Cells: cells,
            UnassignedGrades: unassigned ?? [],
            BucketAverages: new Dictionary<int, decimal?>(),
            SubjectAverage: null,
            FinalGradeOverride: null);

    private static GradeSheetCell CreateCell(int assessmentId, GradeSheetCellState state, GradeSheetGrade? grade = null) =>
        new(assessmentId, state, grade);

    private static GradeSheetBucket CreateBucket(int id, decimal weight, params GradeSheetAssessment[] assessments) =>
        new(id, "Tests", weight, id, assessments);

    private static GradeSheetAssessment CreateAssessment(int id) =>
        new(id, "Test", SeptemberFirst, null);

    private static GradeSheetGrade CreateGrade(int bucketId, decimal value, DateTime? voidedOn = null) =>
        new(
            Id: 100 + bucketId,
            GradeBucketId: bucketId,
            Value: value,
            Token: value.ToString(CultureInfo.InvariantCulture),
            RecordedOn: SeptemberFirst,
            Label: null,
            Comment: null,
            VoidedOn: voidedOn);

    private static T InEnUs<T>(Func<T> create)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            return create();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static void InEnUs(Action action)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
