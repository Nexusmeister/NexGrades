using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.Data.Entities;
using NexGrades.Domain.Export;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

/// <summary>
/// A voided grade, and any grade of a voided assessment, counts nowhere: not in averages, not in the export and
/// not in the close readiness checks.
/// </summary>
public class VoidedGradeExclusionTests
{
    private static readonly DateOnly Day = new(2025, 9, 15);

    private static readonly DateTime Now = new(2026, 3, 1, 10, 0, 0);

    // AverageService

    [Fact]
    public async Task GetBucketAverageAsync_IgnoresAVoidedGrade()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 2.0m, Day);
        await seed.AddGradeAsync(student, 6.0m, Day, voidedOn: Now);

        var average = await new AverageService(db.CreateContextFactory()).GetBucketAverageAsync(student, seed.BucketId);

        Assert.Equal(2.0m, average);
    }

    [Fact]
    public async Task GetBucketAverageAsync_ReturnsNull_WhenEveryGradeIsVoided()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 6.0m, Day, voidedOn: Now);

        var average = await new AverageService(db.CreateContextFactory()).GetBucketAverageAsync(student, seed.BucketId);

        Assert.Null(average);
    }

    [Fact]
    public async Task GetBucketAverageAsync_IgnoresGradesOfAVoidedAssessment()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var active = await seed.AddAssessmentAsync("Test one", Day);
        var voided = await seed.AddAssessmentAsync("Test two", Day, voidedOn: Now);
        await seed.AddGradeAsync(student, 2.0m, Day, active);
        await seed.AddGradeAsync(student, 6.0m, Day, voided);

        var average = await new AverageService(db.CreateContextFactory()).GetBucketAverageAsync(student, seed.BucketId);

        Assert.Equal(2.0m, average);
    }

    [Fact]
    public async Task GetSubjectAverageAsync_IgnoresVoidedGradesAndGradesOfAVoidedAssessment()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db, bucketWeight: 50m);
        var oral = await seed.AddBucketAsync("Oral", 50m, 2);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var voidedAssessment = await seed.AddAssessmentAsync("Test two", Day, oral, voidedOn: Now);
        await seed.AddGradeAsync(student, 2.0m, Day);
        await seed.AddGradeAsync(student, 6.0m, Day, voidedOn: Now);
        await seed.AddGradeAsync(student, 6.0m, Day, voidedAssessment);

        var average = await new AverageService(db.CreateContextFactory()).GetSubjectAverageAsync(student, seed.SubjectInstanceId);

        // The oral bucket has no counting grade, so it has no average and drops out of the weighting.
        Assert.Equal(2.0m, average);
    }

    [Fact]
    public async Task GetSubjectAverageAsync_ReturnsNull_WhenEveryGradeIsVoided()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 2.0m, Day, voidedOn: Now);

        var average = await new AverageService(db.CreateContextFactory()).GetSubjectAverageAsync(student, seed.SubjectInstanceId);

        Assert.Null(average);
    }

    // SubjectOverviewExportService

    private static Task<SubjectOverviewTable> BuildExportAsync(SqliteTestDatabase db, GradeSheetSeed seed) =>
        new SubjectOverviewExportService(db.CreateContextFactory()).BuildAsync(seed.SubjectInstanceId);

    [Fact]
    public async Task BuildAsync_OmitsAVoidedGradeFromTheCellAndTheAverages()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 2.0m, Day);
        await seed.AddGradeAsync(student, 6.0m, new DateOnly(2025, 9, 16), voidedOn: Now);

        var row = Assert.Single((await BuildExportAsync(db, seed)).Rows);

        Assert.Equal("2", row.Cells[1].Text);
        Assert.Equal(2.0m, row.Cells[2].Number);
        Assert.Equal(2.0m, row.Cells[3].Number);
    }

    [Fact]
    public async Task BuildAsync_ShowsEmptyAverages_WhenEveryGradeIsVoided()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 6.0m, Day, voidedOn: Now);

        var row = Assert.Single((await BuildExportAsync(db, seed)).Rows);

        Assert.Equal(SubjectOverviewCell.Empty, row.Cells[1]);
        Assert.Equal(SubjectOverviewCell.Empty, row.Cells[2]);
        Assert.Equal(SubjectOverviewCell.Empty, row.Cells[3]);
    }

    [Fact]
    public async Task BuildAsync_OmitsGradesOfAVoidedAssessment()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var active = await seed.AddAssessmentAsync("Test one", Day);
        var voided = await seed.AddAssessmentAsync("Test two", Day, voidedOn: Now);
        await seed.AddGradeAsync(student, 2.0m, Day, active);
        await seed.AddGradeAsync(student, 6.0m, Day, voided);

        var row = Assert.Single((await BuildExportAsync(db, seed)).Rows);

        Assert.Equal("2", row.Cells[1].Text);
        Assert.Equal(2.0m, row.Cells[3].Number);
    }

    [Fact]
    public async Task BuildAsync_DropsALeftStudent_WhoseOnlyGradesAreVoided()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        await seed.AddStudentAsync("Test", "Alpha");
        var leftVoided = await seed.AddStudentAsync("Test", "Beta", leftOn: new DateTime(2025, 12, 1));
        var leftGraded = await seed.AddStudentAsync("Test", "Gamma", leftOn: new DateTime(2025, 12, 1));
        await seed.AddGradeAsync(leftVoided, 3.0m, Day, voidedOn: Now);
        await seed.AddGradeAsync(leftGraded, 3.0m, Day);

        var table = await BuildExportAsync(db, seed);

        Assert.Equal(["Alpha, Test", "Gamma, Test"], [.. table.Rows.Select(r => r.Cells[0].Text!)]);
    }

    // YearCloseService readiness

    private static async Task MarkClassRolledForwardAsync(SqliteTestDatabase db, GradeSheetSeed seed)
    {
        await using var context = db.CreateContext();
        var classInstance = await context.ClassInstances.SingleAsync(ci => ci.Id == seed.ClassInstanceId);
        classInstance.Status = ClassInstanceStatus.RolledForward;
        await context.SaveChangesAsync();
    }

    private static YearCloseService CreateCloseService(SqliteTestDatabase db) =>
        new(db.CreateContextFactory(), new FixedClock(Now), NullLogger<YearCloseService>.Instance);

    [Fact]
    public async Task GetCloseReadinessAsync_StillWarnsStudentWithoutGrade_WhenTheOnlyGradeIsVoided()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        await MarkClassRolledForwardAsync(db, seed);
        var graded = await seed.AddStudentAsync("Test", "Alpha");
        var onlyVoided = await seed.AddStudentAsync("Test", "Beta");
        await seed.AddGradeAsync(graded, 2.0m, Day);
        await seed.AddGradeAsync(onlyVoided, 3.0m, Day, voidedOn: Now);

        var readiness = await CreateCloseService(db).GetCloseReadinessAsync(seed.YearId);

        var warning = Assert.Single(readiness.Warnings);
        Assert.Equal(CloseReadinessIssueKind.StudentWithoutGrade, warning.Kind);
        Assert.Equal(onlyVoided, warning.StudentId);
    }

    [Fact]
    public async Task GetCloseReadinessAsync_StillWarnsStudentWithoutGrade_WhenTheOnlyGradeBelongsToAVoidedAssessment()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        await MarkClassRolledForwardAsync(db, seed);
        var graded = await seed.AddStudentAsync("Test", "Alpha");
        var other = await seed.AddStudentAsync("Test", "Beta");
        var active = await seed.AddAssessmentAsync("Test one", Day);
        var voided = await seed.AddAssessmentAsync("Test two", Day, voidedOn: Now);
        await seed.AddGradeAsync(graded, 2.0m, Day, active);
        await seed.AddGradeAsync(other, 3.0m, Day, voided);

        var readiness = await CreateCloseService(db).GetCloseReadinessAsync(seed.YearId);

        var warning = Assert.Single(readiness.Warnings);
        Assert.Equal(CloseReadinessIssueKind.StudentWithoutGrade, warning.Kind);
        Assert.Equal(other, warning.StudentId);
    }

    [Fact]
    public async Task GetCloseReadinessAsync_WarnsSubjectWithoutGrades_WhenEveryGradeIsVoided()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        await MarkClassRolledForwardAsync(db, seed);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 2.0m, Day, voidedOn: Now);

        var readiness = await CreateCloseService(db).GetCloseReadinessAsync(seed.YearId);

        Assert.Contains(readiness.Warnings, w => w.Kind == CloseReadinessIssueKind.SubjectWithoutGrades);
    }

    [Fact]
    public async Task GetCloseReadinessAsync_DoesNotWarn_WhenEachStudentHasACountingGrade()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        await MarkClassRolledForwardAsync(db, seed);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessment = await seed.AddAssessmentAsync("Test one", Day);
        await seed.AddGradeAsync(student, 2.0m, Day, assessment);
        await seed.AddGradeAsync(student, 6.0m, Day, voidedOn: Now);

        var readiness = await CreateCloseService(db).GetCloseReadinessAsync(seed.YearId);

        Assert.Empty(readiness.Warnings);
    }
}
