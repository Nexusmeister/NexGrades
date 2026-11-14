using Microsoft.EntityFrameworkCore;

namespace NexGrades.Tests;

public class AssessmentDataTests
{
    private static readonly DateOnly Day = new(2025, 9, 15);

    [Fact]
    public async Task Assessment_RoundTrips_WithTitleDateAndBucket()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var id = await seed.AddAssessmentAsync("Test one", Day);

        await using var context = db.CreateContext();
        var assessment = await context.Assessments.Include(a => a.GradeBucket).SingleAsync(a => a.Id == id);

        Assert.Equal("Test one", assessment.Title);
        Assert.Equal(Day, assessment.Date);
        Assert.Equal(seed.BucketId, assessment.GradeBucketId);
        Assert.Equal(seed.BucketId, assessment.GradeBucket.Id);
        Assert.Null(assessment.VoidedOn);
    }

    [Fact]
    public async Task Assessment_RoundTrips_VoidedOn()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var voidedOn = new DateTime(2025, 10, 1, 8, 30, 0);
        var id = await seed.AddAssessmentAsync("Test one", Day, voidedOn: voidedOn);

        await using var context = db.CreateContext();
        var assessment = await context.Assessments.SingleAsync(a => a.Id == id);

        Assert.Equal(voidedOn, assessment.VoidedOn);
    }

    [Fact]
    public async Task Grade_RoundTrips_AssessmentIdAndVoidedOn()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);
        var voidedOn = new DateTime(2025, 10, 1);
        var gradeId = await seed.AddGradeAsync(student, 2.3m, Day, assessmentId, voidedOn: voidedOn);

        await using var context = db.CreateContext();
        var grade = await context.Grades.SingleAsync(g => g.Id == gradeId);

        Assert.Equal(assessmentId, grade.AssessmentId);
        Assert.Equal(voidedOn, grade.VoidedOn);
        Assert.Equal(2.3m, grade.Value);
    }

    [Fact]
    public async Task SaveChangesAsync_Throws_ForTwoActiveGradesOfOneStudentInOneAssessment()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);
        await seed.AddGradeAsync(student, 2.0m, Day, assessmentId);

        await Assert.ThrowsAsync<DbUpdateException>(() => seed.AddGradeAsync(student, 3.0m, Day, assessmentId));

        await using var verify = db.CreateContext();
        Assert.Equal(1, await verify.Grades.CountAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_Succeeds_ForVoidedGradesNextToAnActiveOne()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);
        await seed.AddGradeAsync(student, 2.0m, Day, assessmentId, voidedOn: new DateTime(2025, 10, 1));
        await seed.AddGradeAsync(student, 3.0m, Day, assessmentId, voidedOn: new DateTime(2025, 10, 2));

        await seed.AddGradeAsync(student, 1.0m, Day, assessmentId);

        await using var verify = db.CreateContext();
        Assert.Equal(3, await verify.Grades.CountAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_Throws_WhenRestoringAGradeNextToAnActiveOne()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);
        var voided = await seed.AddGradeAsync(student, 2.0m, Day, assessmentId, voidedOn: new DateTime(2025, 10, 1));
        await seed.AddGradeAsync(student, 1.0m, Day, assessmentId);

        await using var context = db.CreateContext();
        var grade = await context.Grades.SingleAsync(g => g.Id == voided);
        grade.VoidedOn = null;

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_Succeeds_ForActiveGradesOfDifferentStudentsInOneAssessment()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var first = await seed.AddStudentAsync("Test", "Alpha");
        var second = await seed.AddStudentAsync("Test", "Beta");
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);

        await seed.AddGradeAsync(first, 2.0m, Day, assessmentId);
        await seed.AddGradeAsync(second, 2.0m, Day, assessmentId);

        await using var verify = db.CreateContext();
        Assert.Equal(2, await verify.Grades.CountAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_Succeeds_ForManyGradesWithoutAnAssessment()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");

        await seed.AddGradeAsync(student, 2.0m, Day);
        await seed.AddGradeAsync(student, 2.0m, Day);
        await seed.AddGradeAsync(student, 3.0m, Day, voidedOn: new DateTime(2025, 10, 1));

        await using var verify = db.CreateContext();
        Assert.Equal(3, await verify.Grades.CountAsync(g => g.AssessmentId == null));
    }
}
