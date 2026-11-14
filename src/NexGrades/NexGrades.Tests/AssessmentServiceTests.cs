using Microsoft.EntityFrameworkCore;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class AssessmentServiceTests
{
    private static readonly DateOnly Day = new(2025, 9, 15);

    private static readonly DateTime Now = new(2025, 10, 1, 10, 0, 0);

    private static AssessmentService CreateService(SqliteTestDatabase db) => new(db.CreateContextFactory(), new FixedClock(Now));

    private static async Task<Assessment> LoadAsync(SqliteTestDatabase db, int id)
    {
        await using var context = db.CreateContext();
        return await context.Assessments.AsNoTracking().SingleAsync(a => a.Id == id);
    }

    private static async Task<Grade> LoadGradeAsync(SqliteTestDatabase db, int id)
    {
        await using var context = db.CreateContext();
        return await context.Grades.AsNoTracking().SingleAsync(g => g.Id == id);
    }

    private static async Task<int> CountAssessmentsAsync(SqliteTestDatabase db)
    {
        await using var context = db.CreateContext();
        return await context.Assessments.CountAsync();
    }

    // CreateAssessmentAsync

    [Fact]
    public async Task CreateAssessmentAsync_Succeeds_AndTrimsTheTitle()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);

        var created = await CreateService(db).CreateAssessmentAsync(seed.BucketId, "  Test one  ", Day);

        var stored = await LoadAsync(db, created.Id);
        Assert.Equal("Test one", stored.Title);
        Assert.Equal(Day, stored.Date);
        Assert.Equal(seed.BucketId, stored.GradeBucketId);
        Assert.Null(stored.VoidedOn);
    }

    [Theory]
    [InlineData("2025-08-01")]
    [InlineData("2026-07-31")]
    public async Task CreateAssessmentAsync_Succeeds_OnTheFirstAndLastDayOfTheYear(string date)
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);

        var created = await CreateService(db).CreateAssessmentAsync(seed.BucketId, "Test one", DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture));

        Assert.NotEqual(0, created.Id);
    }

    [Theory]
    [InlineData("2025-07-31")]
    [InlineData("2026-08-01")]
    public async Task CreateAssessmentAsync_Throws_ForDateOutsideTheYear(string date)
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).CreateAssessmentAsync(seed.BucketId, "Test one", DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Equal(0, await CountAssessmentsAsync(db));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAssessmentAsync_Throws_ForEmptyTitle(string title)
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).CreateAssessmentAsync(seed.BucketId, title, Day));

        Assert.Equal(0, await CountAssessmentsAsync(db));
    }

    [Fact]
    public async Task CreateAssessmentAsync_Succeeds_ForTitleOfExactlyTheMaximumLength()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);

        var created = await CreateService(db).CreateAssessmentAsync(seed.BucketId, new string('x', AssessmentService.MaxTitleLength), Day);

        Assert.NotEqual(0, created.Id);
    }

    [Fact]
    public async Task CreateAssessmentAsync_Throws_ForTooLongTitle()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).CreateAssessmentAsync(seed.BucketId, new string('x', AssessmentService.MaxTitleLength + 1), Day));

        Assert.Equal(0, await CountAssessmentsAsync(db));
    }

    [Fact]
    public async Task CreateAssessmentAsync_Throws_ForMissingBucket()
    {
        using var db = new SqliteTestDatabase();
        await GradeSheetSeed.CreateAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).CreateAssessmentAsync(9999, "Test one", Day));

        Assert.Equal(0, await CountAssessmentsAsync(db));
    }

    // EditAssessmentAsync

    [Fact]
    public async Task EditAssessmentAsync_Succeeds_ForTitleChangeOnly()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var id = await seed.AddAssessmentAsync("Test one", Day);

        await CreateService(db).EditAssessmentAsync(id, "  Test two ", Day);

        var stored = await LoadAsync(db, id);
        Assert.Equal("Test two", stored.Title);
        Assert.Equal(Day, stored.Date);
        Assert.Equal(seed.BucketId, stored.GradeBucketId);
    }

    [Fact]
    public async Task EditAssessmentAsync_Throws_ForMissingAssessment()
    {
        using var db = new SqliteTestDatabase();
        await GradeSheetSeed.CreateAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).EditAssessmentAsync(9999, "Test one", Day));
    }

    [Fact]
    public async Task EditAssessmentAsync_Throws_ForEmptyTitle()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var id = await seed.AddAssessmentAsync("Test one", Day);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).EditAssessmentAsync(id, " ", Day));

        Assert.Equal("Test one", (await LoadAsync(db, id)).Title);
    }

    [Fact]
    public async Task EditAssessmentAsync_Throws_ForTooLongTitle()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var id = await seed.AddAssessmentAsync("Test one", Day);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).EditAssessmentAsync(id, new string('x', AssessmentService.MaxTitleLength + 1), Day));

        Assert.Equal("Test one", (await LoadAsync(db, id)).Title);
    }

    [Fact]
    public async Task EditAssessmentAsync_Throws_ForDateOutsideTheYear()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var id = await seed.AddAssessmentAsync("Test one", Day);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).EditAssessmentAsync(id, "Test one", new DateOnly(2026, 8, 1)));

        Assert.Equal(Day, (await LoadAsync(db, id)).Date);
    }

    [Fact]
    public async Task EditAssessmentAsync_Throws_ForVoidedAssessment()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var id = await seed.AddAssessmentAsync("Test one", Day, voidedOn: Now);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).EditAssessmentAsync(id, "Test two", Day));

        Assert.Equal("Test one", (await LoadAsync(db, id)).Title);
    }

    [Fact]
    public async Task EditAssessmentAsync_MovesGradesStillOnTheOldDate_ToTheNewDate()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var first = await seed.AddStudentAsync("Test", "Alpha");
        var second = await seed.AddStudentAsync("Test", "Beta");
        var id = await seed.AddAssessmentAsync("Test one", Day);
        var firstGrade = await seed.AddGradeAsync(first, 2.0m, Day, id);
        var secondGrade = await seed.AddGradeAsync(second, 3.0m, Day, id);
        var newDate = new DateOnly(2025, 9, 22);

        await CreateService(db).EditAssessmentAsync(id, "Test one", newDate);

        Assert.Equal(newDate, (await LoadAsync(db, id)).Date);
        Assert.Equal(newDate, (await LoadGradeAsync(db, firstGrade)).RecordedOn);
        Assert.Equal(newDate, (await LoadGradeAsync(db, secondGrade)).RecordedOn);
    }

    [Fact]
    public async Task EditAssessmentAsync_LeavesAHandEditedGradeDateAlone()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var first = await seed.AddStudentAsync("Test", "Alpha");
        var second = await seed.AddStudentAsync("Test", "Beta");
        var id = await seed.AddAssessmentAsync("Test one", Day);
        var handEdited = new DateOnly(2025, 9, 18);
        var movedGrade = await seed.AddGradeAsync(first, 2.0m, Day, id);
        var editedGrade = await seed.AddGradeAsync(second, 3.0m, handEdited, id);
        var newDate = new DateOnly(2025, 9, 22);

        await CreateService(db).EditAssessmentAsync(id, "Test one", newDate);

        Assert.Equal(newDate, (await LoadGradeAsync(db, movedGrade)).RecordedOn);
        Assert.Equal(handEdited, (await LoadGradeAsync(db, editedGrade)).RecordedOn);
    }

    [Fact]
    public async Task EditAssessmentAsync_LeavesVoidedGradesAndOtherAssessmentsGradesAlone()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var id = await seed.AddAssessmentAsync("Test one", Day);
        var otherId = await seed.AddAssessmentAsync("Test two", Day);
        var voidedGrade = await seed.AddGradeAsync(student, 2.0m, Day, id, voidedOn: Now);
        var otherGrade = await seed.AddGradeAsync(student, 3.0m, Day, otherId);

        await CreateService(db).EditAssessmentAsync(id, "Test one", new DateOnly(2025, 9, 22));

        Assert.Equal(Day, (await LoadGradeAsync(db, voidedGrade)).RecordedOn);
        Assert.Equal(Day, (await LoadGradeAsync(db, otherGrade)).RecordedOn);
    }

    [Fact]
    public async Task EditAssessmentAsync_Throws_AndChangesNothing_WhenAGradedStudentWouldNotBeEnrolledOnTheNewDate()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var fine = await seed.AddStudentAsync("Test", "Alpha");
        var leaving = await seed.AddStudentAsync("Test", "Beta", leftOn: new DateTime(2025, 9, 20));
        var id = await seed.AddAssessmentAsync("Test one", Day);
        var fineGrade = await seed.AddGradeAsync(fine, 2.0m, Day, id);
        var leavingGrade = await seed.AddGradeAsync(leaving, 3.0m, Day, id);

        var exception = await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).EditAssessmentAsync(id, "Renamed", new DateOnly(2025, 9, 22)));

        Assert.Contains(leaving.ToString(System.Globalization.CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
        var stored = await LoadAsync(db, id);
        Assert.Equal("Test one", stored.Title);
        Assert.Equal(Day, stored.Date);
        Assert.Equal(Day, (await LoadGradeAsync(db, fineGrade)).RecordedOn);
        Assert.Equal(Day, (await LoadGradeAsync(db, leavingGrade)).RecordedOn);
    }

    [Fact]
    public async Task EditAssessmentAsync_Throws_WhenAGradedStudentWouldNotYetBeEnrolledOnTheNewDate()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", joinedOn: new DateTime(2025, 9, 10));
        var id = await seed.AddAssessmentAsync("Test one", Day);
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day, id);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).EditAssessmentAsync(id, "Test one", new DateOnly(2025, 9, 9)));

        Assert.Equal(Day, (await LoadAsync(db, id)).Date);
        Assert.Equal(Day, (await LoadGradeAsync(db, gradeId)).RecordedOn);
    }

    [Fact]
    public async Task EditAssessmentAsync_Succeeds_WhenTheNewDateIsTheStudentsJoinDate()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", joinedOn: new DateTime(2025, 9, 10));
        var id = await seed.AddAssessmentAsync("Test one", Day);
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day, id);

        await CreateService(db).EditAssessmentAsync(id, "Test one", new DateOnly(2025, 9, 10));

        Assert.Equal(new DateOnly(2025, 9, 10), (await LoadGradeAsync(db, gradeId)).RecordedOn);
    }

    [Fact]
    public async Task EditAssessmentAsync_Succeeds_WhenTheNewDateIsTheJoinDay_AndJoinedOnHasATimeOfDay()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", joinedOn: new DateTime(2025, 9, 10, 9, 15, 0));
        var id = await seed.AddAssessmentAsync("Test one", Day);
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day, id);

        await CreateService(db).EditAssessmentAsync(id, "Test one", new DateOnly(2025, 9, 10));

        Assert.Equal(new DateOnly(2025, 9, 10), (await LoadGradeAsync(db, gradeId)).RecordedOn);
    }

    [Fact]
    public async Task EditAssessmentAsync_Throws_WhenTheNewDateIsTheLeaveDay_AndLeftOnHasATimeOfDay()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", leftOn: new DateTime(2025, 9, 20, 14, 30, 0));
        var id = await seed.AddAssessmentAsync("Test one", Day);
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day, id);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).EditAssessmentAsync(id, "Test one", new DateOnly(2025, 9, 20)));

        Assert.Equal(Day, (await LoadAsync(db, id)).Date);
        Assert.Equal(Day, (await LoadGradeAsync(db, gradeId)).RecordedOn);
    }

    [Fact]
    public async Task EditAssessmentAsync_Throws_WhenTheNewDateIsTheStudentsLeaveDate()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", leftOn: new DateTime(2025, 9, 20));
        var id = await seed.AddAssessmentAsync("Test one", Day);
        await seed.AddGradeAsync(student, 2.0m, Day, id);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).EditAssessmentAsync(id, "Test one", new DateOnly(2025, 9, 20)));

        Assert.Equal(Day, (await LoadAsync(db, id)).Date);
    }

    [Fact]
    public async Task EditAssessmentAsync_Throws_WhenAGradedStudentIsExempt()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var id = await seed.AddAssessmentAsync("Test one", Day);
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day, id);
        await seed.AddExemptionAsync(student);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).EditAssessmentAsync(id, "Test one", new DateOnly(2025, 9, 22)));

        Assert.Equal(Day, (await LoadAsync(db, id)).Date);
        Assert.Equal(Day, (await LoadGradeAsync(db, gradeId)).RecordedOn);
    }

    [Fact]
    public async Task EditAssessmentAsync_IgnoresTheEnrollmentOfAStudentWhoseGradeIsVoided()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", leftOn: new DateTime(2025, 9, 20));
        var id = await seed.AddAssessmentAsync("Test one", Day);
        await seed.AddGradeAsync(student, 2.0m, Day, id, voidedOn: Now);

        await CreateService(db).EditAssessmentAsync(id, "Test one", new DateOnly(2025, 9, 22));

        Assert.Equal(new DateOnly(2025, 9, 22), (await LoadAsync(db, id)).Date);
    }

    // VoidAssessmentAsync and RestoreAssessmentAsync

    [Fact]
    public async Task VoidAssessmentAsync_SetsVoidedOnFromTheClock_AndKeepsTheGrades()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var id = await seed.AddAssessmentAsync("Test one", Day);
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day, id);

        await CreateService(db).VoidAssessmentAsync(id);

        Assert.Equal(Now, (await LoadAsync(db, id)).VoidedOn);
        var grade = await LoadGradeAsync(db, gradeId);
        Assert.Null(grade.VoidedOn);
        Assert.Equal(2.0m, grade.Value);
    }

    [Fact]
    public async Task VoidAssessmentAsync_Throws_ForMissingAssessment()
    {
        using var db = new SqliteTestDatabase();
        await GradeSheetSeed.CreateAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).VoidAssessmentAsync(9999));
    }

    [Fact]
    public async Task VoidAssessmentAsync_Throws_WhenCalledTwice()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var id = await seed.AddAssessmentAsync("Test one", Day);
        var service = CreateService(db);
        await service.VoidAssessmentAsync(id);

        await Assert.ThrowsAsync<InvariantViolationException>(() => service.VoidAssessmentAsync(id));
    }

    [Fact]
    public async Task RestoreAssessmentAsync_ClearsVoidedOn_AfterVoiding()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var id = await seed.AddAssessmentAsync("Test one", Day);
        var service = CreateService(db);
        await service.VoidAssessmentAsync(id);

        await service.RestoreAssessmentAsync(id);

        Assert.Null((await LoadAsync(db, id)).VoidedOn);
    }

    [Fact]
    public async Task RestoreAssessmentAsync_KeepsAGradeVoidedOnItsOwnVoided()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var id = await seed.AddAssessmentAsync("Test one", Day, voidedOn: Now);
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day, id, voidedOn: Now);

        await CreateService(db).RestoreAssessmentAsync(id);

        Assert.Equal(Now, (await LoadGradeAsync(db, gradeId)).VoidedOn);
    }

    [Fact]
    public async Task RestoreAssessmentAsync_Throws_ForAssessmentThatIsNotVoided()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var id = await seed.AddAssessmentAsync("Test one", Day);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RestoreAssessmentAsync(id));
    }

    [Fact]
    public async Task RestoreAssessmentAsync_Throws_ForMissingAssessment()
    {
        using var db = new SqliteTestDatabase();
        await GradeSheetSeed.CreateAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RestoreAssessmentAsync(9999));
    }
}
