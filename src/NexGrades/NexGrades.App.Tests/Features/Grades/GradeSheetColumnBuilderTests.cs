using System.Globalization;
using NexGrades.App.Features.Grades;
using NexGrades.Domain.Grading;

namespace NexGrades.App.Tests.Features.Grades;

public class GradeSheetColumnBuilderTests
{
    private static readonly DateOnly SeptemberFirst = new(2025, 9, 3);

    [Fact]
    public void Build_ReturnsStudentAndAverageOnly_ForSheetWithoutBuckets()
    {
        var columns = BuildIn("en-US", CreateSheet([], CreateStudent(1)));

        Assert.Equal(
            [
                GradeSheetColumnKind.Student,
                GradeSheetColumnKind.SubjectAverage,
                GradeSheetColumnKind.ProposedFinalGrade,
                GradeSheetColumnKind.FinalGrade,
            ],
            columns.Select(c => c.Kind).ToList());
    }

    [Fact]
    public void Build_PlacesStudentColumnFirst_WithStudentHeader()
    {
        var columns = BuildIn("en-US", CreateSheet([], CreateStudent(1)));

        Assert.Equal(GradeSheetColumnKind.Student, columns[0].Kind);
        Assert.Equal("Student", columns[0].Header);
    }

    [Fact]
    public void Build_ReturnsColumnsInDisplayOrder_ForTwoBuckets()
    {
        var sheet = CreateSheet(
            [
                CreateBucket(10, "Tests", 0, CreateAssessment(1, "Test 1"), CreateAssessment(2, "Test 2")),
                CreateBucket(20, "Oral", 1, CreateAssessment(3, "Presentation")),
            ],
            CreateStudent(1));

        var kinds = BuildIn("en-US", sheet).Select(c => c.Kind).ToList();

        Assert.Equal(
            [
                GradeSheetColumnKind.Student,
                GradeSheetColumnKind.Assessment,
                GradeSheetColumnKind.Assessment,
                GradeSheetColumnKind.BucketAverage,
                GradeSheetColumnKind.Assessment,
                GradeSheetColumnKind.BucketAverage,
                GradeSheetColumnKind.SubjectAverage,
                GradeSheetColumnKind.ProposedFinalGrade,
                GradeSheetColumnKind.FinalGrade,
            ],
            kinds);
    }

    [Fact]
    public void Build_ReturnsOnlyBucketAverage_ForBucketWithoutAssessments()
    {
        var sheet = CreateSheet([CreateBucket(10, "Tests", 0)], CreateStudent(1));

        var kinds = BuildIn("en-US", sheet).Select(c => c.Kind).ToList();

        Assert.Equal(
            [
                GradeSheetColumnKind.Student,
                GradeSheetColumnKind.BucketAverage,
                GradeSheetColumnKind.SubjectAverage,
                GradeSheetColumnKind.ProposedFinalGrade,
                GradeSheetColumnKind.FinalGrade,
            ],
            kinds);
    }

    [Fact]
    public void Build_UsesAssessmentTitleAsHeader()
    {
        var sheet = CreateSheet([CreateBucket(10, "Tests", 0, CreateAssessment(1, "Test 1"))], CreateStudent(1));

        var column = BuildIn("en-US", sheet).Single(c => c.Kind == GradeSheetColumnKind.Assessment);

        Assert.Equal("Test 1", column.Header);
    }

    [Fact]
    public void Build_FormatsAssessmentDateAsSubHeader_ForEnUsCulture()
    {
        var sheet = CreateSheet([CreateBucket(10, "Tests", 0, CreateAssessment(1, "Test 1"))], CreateStudent(1));

        var column = BuildIn("en-US", sheet).Single(c => c.Kind == GradeSheetColumnKind.Assessment);

        Assert.Equal("9/3/2025", column.SubHeader);
    }

    [Fact]
    public void Build_FormatsAssessmentDateAsSubHeader_ForDeDeCulture()
    {
        var sheet = CreateSheet([CreateBucket(10, "Tests", 0, CreateAssessment(1, "Test 1"))], CreateStudent(1));

        var column = BuildIn("de-DE", sheet).Single(c => c.Kind == GradeSheetColumnKind.Assessment);

        Assert.Equal(SeptemberFirst.ToString("d", CultureInfo.GetCultureInfo("de-DE")), column.SubHeader);
        Assert.NotEqual("9/3/2025", column.SubHeader);
    }

    [Fact]
    public void Build_CarriesAssessmentIdAndBucket_InAssessmentColumn()
    {
        var sheet = CreateSheet([CreateBucket(10, "Tests", 0, CreateAssessment(7, "Test 1"))], CreateStudent(1));

        var column = BuildIn("en-US", sheet).Single(c => c.Kind == GradeSheetColumnKind.Assessment);

        Assert.Equal(7, column.AssessmentId);
        Assert.Equal(10, column.BucketId);
        Assert.Equal("Tests", column.BucketName);
        Assert.Equal(SeptemberFirst, column.AssessmentDate);
    }

    [Fact]
    public void Build_NumbersCellIndexesAcrossBuckets_InColumnOrder()
    {
        var sheet = CreateSheet(
            [
                CreateBucket(10, "Tests", 0, CreateAssessment(1, "Test 1"), CreateAssessment(2, "Test 2")),
                CreateBucket(20, "Oral", 1, CreateAssessment(3, "Presentation")),
            ],
            CreateStudent(1));

        var assessments = BuildIn("en-US", sheet).Where(c => c.Kind == GradeSheetColumnKind.Assessment).ToList();

        Assert.Equal([0, 1, 2], assessments.Select(c => c.CellIndex).ToList());
    }

    [Fact]
    public void Build_NumbersBucketIndexes_ForAssessmentAndBucketColumns()
    {
        var sheet = CreateSheet(
            [
                CreateBucket(10, "Tests", 0, CreateAssessment(1, "Test 1"), CreateAssessment(2, "Test 2")),
                CreateBucket(20, "Oral", 1, CreateAssessment(3, "Presentation")),
            ],
            CreateStudent(1));

        var columns = BuildIn("en-US", sheet);

        Assert.Equal(0, columns[1].BucketIndex);
        Assert.Equal(0, columns[2].BucketIndex);
        Assert.Equal(0, columns[3].BucketIndex);
        Assert.Equal(1, columns[4].BucketIndex);
        Assert.Equal(1, columns[5].BucketIndex);
    }

    [Fact]
    public void Build_NamesBucketAverageColumn_WithBucketName()
    {
        var sheet = CreateSheet([CreateBucket(10, "Tests", 0, CreateAssessment(1, "Test 1"))], CreateStudent(1));

        var column = BuildIn("en-US", sheet).Single(c => c.Kind == GradeSheetColumnKind.BucketAverage);

        Assert.Equal("Tests: average", column.Header);
        Assert.Equal(10, column.BucketId);
    }

    [Fact]
    public void Build_HeadsSubjectAverageColumn_WithAverage()
    {
        var sheet = CreateSheet([CreateBucket(10, "Tests", 0)], CreateStudent(1));

        var columns = BuildIn("en-US", sheet);
        var column = columns.Single(c => c.Kind == GradeSheetColumnKind.SubjectAverage);

        Assert.Equal("Average", column.Header);
    }

    [Fact]
    public void Build_AppendsFinalGradeColumns_AfterSubjectAverage()
    {
        var columns = BuildIn("en-US", CreateSheet([CreateBucket(10, "Tests", 0)], CreateStudent(1)));

        Assert.Equal(GradeSheetColumnKind.ProposedFinalGrade, columns[^2].Kind);
        Assert.Equal("Proposal", columns[^2].Header);
        Assert.Equal(GradeSheetColumnKind.FinalGrade, columns[^1].Kind);
        Assert.Equal("Final grade", columns[^1].Header);
    }

    [Fact]
    public void Build_WritesGermanHeaders_ForGermanUiCulture()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var sheet = CreateSheet([CreateBucket(10, "Tests", 0, CreateAssessment(1, "Test 1"))], CreateStudent(1));

            var columns = BuildIn("de-DE", sheet);

            Assert.Equal("Schülerin / Schüler", columns[0].Header);
            Assert.Equal("Tests: Durchschnitt", columns.Single(c => c.Kind == GradeSheetColumnKind.BucketAverage).Header);
            Assert.Equal("Durchschnitt", columns.Single(c => c.Kind == GradeSheetColumnKind.SubjectAverage).Header);
            Assert.Equal("Vorschlag", columns.Single(c => c.Kind == GradeSheetColumnKind.ProposedFinalGrade).Header);
            Assert.Equal("Zeugnisnote", columns[^1].Header);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Build_MakesFinalGradeColumnEditable_AndProposalColumnReadOnly()
    {
        var columns = BuildIn("en-US", CreateSheet([], CreateStudent(1)));

        Assert.True(columns.Single(c => c.Kind == GradeSheetColumnKind.FinalGrade).IsEditable);
        Assert.False(columns.Single(c => c.Kind == GradeSheetColumnKind.ProposedFinalGrade).IsEditable);
    }

    [Fact]
    public void BindingPath_PointsAtFinalGradeText_ForFinalGradeColumn()
    {
        var columns = BuildIn("en-US", CreateSheet([], CreateStudent(1)));

        Assert.Equal("FinalGrade.Text", columns.Single(c => c.Kind == GradeSheetColumnKind.FinalGrade).BindingPath);
        Assert.Equal("ProposalText", columns.Single(c => c.Kind == GradeSheetColumnKind.ProposedFinalGrade).BindingPath);
    }

    [Fact]
    public void Build_OmitsOtherGradesColumn_WhenNoStudentHasUnassignedGrades()
    {
        var sheet = CreateSheet(
            [CreateBucket(10, "Tests", 0, CreateAssessment(1, "Test 1"))],
            CreateStudent(1));

        var kinds = BuildIn("en-US", sheet).Select(c => c.Kind);

        Assert.DoesNotContain(GradeSheetColumnKind.OtherGrades, kinds);
    }

    [Fact]
    public void Build_AddsOtherGradesColumnAfterAssessments_WhenBucketHasUnassignedGrade()
    {
        var sheet = CreateSheet(
            [CreateBucket(10, "Tests", 0, CreateAssessment(1, "Test 1"))],
            CreateStudent(1, CreateUnassignedGrade(10)));

        var kinds = BuildIn("en-US", sheet).Select(c => c.Kind).ToList();

        Assert.Equal(
            [
                GradeSheetColumnKind.Student,
                GradeSheetColumnKind.Assessment,
                GradeSheetColumnKind.OtherGrades,
                GradeSheetColumnKind.BucketAverage,
                GradeSheetColumnKind.SubjectAverage,
                GradeSheetColumnKind.ProposedFinalGrade,
                GradeSheetColumnKind.FinalGrade,
            ],
            kinds);
    }

    [Fact]
    public void Build_NamesOtherGradesColumn_WithBucketName()
    {
        var sheet = CreateSheet(
            [CreateBucket(10, "Tests", 0, CreateAssessment(1, "Test 1"))],
            CreateStudent(1, CreateUnassignedGrade(10)));

        var column = BuildIn("en-US", sheet).Single(c => c.Kind == GradeSheetColumnKind.OtherGrades);

        Assert.Equal("Tests: other grades", column.Header);
        Assert.Equal(10, column.BucketId);
    }

    [Fact]
    public void Build_AddsOtherGradesColumn_WhenOnlyOneOfManyStudentsHasUnassignedGrade()
    {
        var sheet = CreateSheet(
            [CreateBucket(10, "Tests", 0)],
            CreateStudent(1),
            CreateStudent(2, CreateUnassignedGrade(10)));

        var kinds = BuildIn("en-US", sheet).Select(c => c.Kind);

        Assert.Contains(GradeSheetColumnKind.OtherGrades, kinds);
    }

    [Fact]
    public void Build_OmitsOtherGradesColumnForBucket_WhenUnassignedGradeBelongsToAnotherBucket()
    {
        var sheet = CreateSheet(
            [
                CreateBucket(10, "Tests", 0, CreateAssessment(1, "Test 1")),
                CreateBucket(20, "Oral", 1, CreateAssessment(2, "Presentation")),
            ],
            CreateStudent(1, CreateUnassignedGrade(20)));

        var otherGrades = BuildIn("en-US", sheet).Where(c => c.Kind == GradeSheetColumnKind.OtherGrades).ToList();

        Assert.Single(otherGrades);
        Assert.Equal(20, otherGrades[0].BucketId);
    }

    [Fact]
    public void Build_FlagsColumnAsVoided_WhenAssessmentIsVoided()
    {
        var voidedAssessment = CreateAssessment(1, "Test 1", new DateTime(2025, 10, 1, 8, 0, 0));
        var sheet = CreateSheet([CreateBucket(10, "Tests", 0, voidedAssessment)], CreateStudent(1));

        var column = BuildIn("en-US", sheet).Single(c => c.Kind == GradeSheetColumnKind.Assessment);

        Assert.True(column.IsVoided);
    }

    [Fact]
    public void Build_LeavesColumnNotVoided_WhenAssessmentIsActive()
    {
        var sheet = CreateSheet([CreateBucket(10, "Tests", 0, CreateAssessment(1, "Test 1"))], CreateStudent(1));

        var column = BuildIn("en-US", sheet).Single(c => c.Kind == GradeSheetColumnKind.Assessment);

        Assert.False(column.IsVoided);
    }

    [Fact]
    public void Build_Throws_ForNullSheet()
    {
        Assert.Throws<ArgumentNullException>(() => GradeSheetColumnBuilder.Build((GradeSheet)null!));
    }

    private static IReadOnlyList<GradeSheetColumn> BuildIn(string cultureName, GradeSheet sheet)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        try
        {
            return GradeSheetColumnBuilder.Build(sheet);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static GradeSheet CreateSheet(IReadOnlyList<GradeSheetBucket> buckets, params GradeSheetStudent[] students) =>
        new(
            SubjectInstanceId: 1,
            SubjectName: "Mathematik",
            ClassName: "5A",
            SchoolYearId: 1,
            SchoolYearLabel: "2025/26",
            IsWritable: true,
            IsClosed: false,
            UnlockedUntil: null,
            Buckets: buckets,
            Students: students);

    private static GradeSheetBucket CreateBucket(int id, string name, int sortOrder, params GradeSheetAssessment[] assessments) =>
        new(id, name, 1m, sortOrder, assessments);

    private static GradeSheetAssessment CreateAssessment(int id, string title, DateTime? voidedOn = null) =>
        new(id, title, SeptemberFirst, voidedOn);

    private static GradeSheetStudent CreateStudent(int id, params GradeSheetGrade[] unassignedGrades) =>
        new(
            StudentId: id,
            FirstName: "Ada",
            LastName: "Lovelace",
            JoinedOn: new DateOnly(2025, 8, 1),
            LeftOn: null,
            IsExempt: false,
            Cells: [],
            UnassignedGrades: unassignedGrades,
            BucketAverages: new Dictionary<int, decimal?>(),
            SubjectAverage: null,
            FinalGradeOverride: null);

    private static GradeSheetGrade CreateUnassignedGrade(int bucketId) =>
        new(
            Id: 100 + bucketId,
            GradeBucketId: bucketId,
            Value: 2m,
            Token: "2",
            RecordedOn: new DateOnly(2025, 10, 1),
            Label: null,
            Comment: null,
            VoidedOn: null);
}
