using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class GradeServiceAssessmentTests
{
    private static readonly DateOnly Day = new(2025, 9, 15);

    private static readonly DateTime Now = new(2025, 10, 1, 10, 0, 0);

    private static GradeService CreateService(SqliteTestDatabase db) => new(db.CreateContextFactory(), new FixedClock(Now));

    private static async Task<Grade> LoadGradeAsync(SqliteTestDatabase db, int gradeId)
    {
        await using var context = db.CreateContext();
        return await context.Grades.AsNoTracking().SingleAsync(g => g.Id == gradeId);
    }

    private static async Task<int> CountGradesAsync(SqliteTestDatabase db)
    {
        await using var context = db.CreateContext();
        return await context.Grades.CountAsync();
    }

    // RecordAssessmentGradeAsync

    [Fact]
    public async Task RecordAssessmentGradeAsync_Succeeds_AndUsesTheAssessmentDateAndBucket()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);

        var grade = await CreateService(db).RecordAssessmentGradeAsync(assessmentId, student, 2.3m, "  Well done  ");

        var stored = await LoadGradeAsync(db, grade.Id);
        Assert.Equal(assessmentId, stored.AssessmentId);
        Assert.Equal(seed.BucketId, stored.GradeBucketId);
        Assert.Equal(student, stored.StudentId);
        Assert.Equal(2.3m, stored.Value);
        Assert.Equal(Day, stored.RecordedOn);
        Assert.Equal("Well done", stored.Comment);
        Assert.Null(stored.VoidedOn);
    }

    [Fact]
    public async Task RecordAssessmentGradeAsync_Succeeds_OnTheJoinDate()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", joinedOn: new DateTime(2025, 9, 15));
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);

        var grade = await CreateService(db).RecordAssessmentGradeAsync(assessmentId, student, 2.0m);

        Assert.NotEqual(0, grade.Id);
    }

    [Fact]
    public async Task RecordAssessmentGradeAsync_Throws_ForIllegalGradeValue()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).RecordAssessmentGradeAsync(assessmentId, student, 2.5m));

        Assert.Equal(0, await CountGradesAsync(db));
    }

    [Fact]
    public async Task RecordAssessmentGradeAsync_Throws_ForMissingAssessment()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).RecordAssessmentGradeAsync(9999, student, 2.0m));

        Assert.Equal(0, await CountGradesAsync(db));
    }

    [Fact]
    public async Task RecordAssessmentGradeAsync_Throws_ForVoidedAssessment()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day, voidedOn: Now);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).RecordAssessmentGradeAsync(assessmentId, student, 2.0m));

        Assert.Equal(0, await CountGradesAsync(db));
    }

    [Fact]
    public async Task RecordAssessmentGradeAsync_Throws_ForStudentNotYetEnrolledOnTheAssessmentDate()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", joinedOn: new DateTime(2025, 9, 16));
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).RecordAssessmentGradeAsync(assessmentId, student, 2.0m));

        Assert.Equal(0, await CountGradesAsync(db));
    }

    [Fact]
    public async Task RecordAssessmentGradeAsync_Throws_ForStudentWhoLeftOnTheAssessmentDate()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", leftOn: new DateTime(2025, 9, 15));
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);

        // LeftOn is exclusive: the student is no longer enrolled on the day they left.
        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).RecordAssessmentGradeAsync(assessmentId, student, 2.0m));

        Assert.Equal(0, await CountGradesAsync(db));
    }

    [Fact]
    public async Task RecordAssessmentGradeAsync_Succeeds_ForStudentWhoLeavesTheDayAfterTheAssessment()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", leftOn: new DateTime(2025, 9, 16));
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);

        var grade = await CreateService(db).RecordAssessmentGradeAsync(assessmentId, student, 2.0m);

        Assert.NotEqual(0, grade.Id);
    }

    [Fact]
    public async Task RecordAssessmentGradeAsync_Throws_ForExemptStudent()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddExemptionAsync(student);
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).RecordAssessmentGradeAsync(assessmentId, student, 2.0m));

        Assert.Equal(0, await CountGradesAsync(db));
    }

    [Fact]
    public async Task RecordAssessmentGradeAsync_Throws_ForDuplicateActiveGrade()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);
        await seed.AddGradeAsync(student, 2.0m, Day, assessmentId);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).RecordAssessmentGradeAsync(assessmentId, student, 3.0m));

        Assert.Equal(1, await CountGradesAsync(db));
    }

    [Fact]
    public async Task RecordAssessmentGradeAsync_Succeeds_WhenTheEarlierGradeIsVoided()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);
        await seed.AddGradeAsync(student, 2.0m, Day, assessmentId, voidedOn: Now);

        var grade = await CreateService(db).RecordAssessmentGradeAsync(assessmentId, student, 3.0m);

        Assert.NotEqual(0, grade.Id);
        Assert.Equal(2, await CountGradesAsync(db));
    }

    [Fact]
    public async Task RecordAssessmentGradeAsync_Throws_ForTooLongComment()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).RecordAssessmentGradeAsync(
                assessmentId, student, 2.0m, new string('x', GradeService.MaxCommentLength + 1)));

        Assert.Equal(0, await CountGradesAsync(db));
    }

    // Enrollment day rule and school year dates

    [Fact]
    public async Task RecordGradeAsync_Succeeds_OnTheJoinDay_WhenJoinedOnHasATimeOfDay()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", joinedOn: new DateTime(2025, 9, 15, 9, 15, 0));

        var grade = await CreateService(db).RecordGradeAsync(seed.BucketId, student, 2.0m, Day);

        Assert.Equal(Day, (await LoadGradeAsync(db, grade.Id)).RecordedOn);
    }

    [Fact]
    public async Task RecordGradeAsync_Throws_TheDayBeforeTheJoinDay_WhenJoinedOnHasATimeOfDay()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", joinedOn: new DateTime(2025, 9, 15, 9, 15, 0));

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).RecordGradeAsync(seed.BucketId, student, 2.0m, Day.AddDays(-1)));

        Assert.Equal(0, await CountGradesAsync(db));
    }

    [Fact]
    public async Task RecordAssessmentGradeAsync_Succeeds_OnTheJoinDay_WhenJoinedOnHasATimeOfDay()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", joinedOn: new DateTime(2025, 9, 15, 9, 15, 0));
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);

        var grade = await CreateService(db).RecordAssessmentGradeAsync(assessmentId, student, 2.0m);

        Assert.Equal(Day, (await LoadGradeAsync(db, grade.Id)).RecordedOn);
    }

    [Fact]
    public async Task UpdateGradeAsync_Succeeds_ForNewDateOnTheJoinDay_WhenJoinedOnHasATimeOfDay()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", joinedOn: new DateTime(2025, 9, 10, 9, 15, 0));
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day);

        await CreateService(db).UpdateGradeAsync(gradeId, 2.0m, new DateOnly(2025, 9, 10));

        Assert.Equal(new DateOnly(2025, 9, 10), (await LoadGradeAsync(db, gradeId)).RecordedOn);
    }

    [Fact]
    public async Task UpdateGradeAsync_Throws_ForNewDateOnTheLeaveDay_WhenLeftOnHasATimeOfDay()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", leftOn: new DateTime(2025, 9, 20, 14, 30, 0));
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).UpdateGradeAsync(gradeId, 2.0m, new DateOnly(2025, 9, 20)));

        Assert.Equal(Day, (await LoadGradeAsync(db, gradeId)).RecordedOn);
    }

    [Theory]
    [InlineData("2025-08-01")]
    [InlineData("2026-07-31")]
    public async Task RecordGradeAsync_Succeeds_OnTheFirstAndLastDayOfTheYear(string date)
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var day = DateOnly.Parse(date, CultureInfo.InvariantCulture);

        var grade = await CreateService(db).RecordGradeAsync(seed.BucketId, student, 2.0m, day);

        Assert.Equal(day, (await LoadGradeAsync(db, grade.Id)).RecordedOn);
    }

    [Theory]
    [InlineData("2025-07-31")]
    [InlineData("2026-08-01")]
    public async Task RecordGradeAsync_Throws_ForDateOutsideTheYear(string date)
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", joinedOn: new DateTime(2025, 1, 1));
        var day = DateOnly.Parse(date, CultureInfo.InvariantCulture);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).RecordGradeAsync(seed.BucketId, student, 2.0m, day));

        Assert.Equal(0, await CountGradesAsync(db));
    }

    [Theory]
    [InlineData("2025-08-01")]
    [InlineData("2026-07-31")]
    public async Task UpdateGradeAsync_Succeeds_OnTheFirstAndLastDayOfTheYear(string date)
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day);
        var day = DateOnly.Parse(date, CultureInfo.InvariantCulture);

        await CreateService(db).UpdateGradeAsync(gradeId, 2.0m, day);

        Assert.Equal(day, (await LoadGradeAsync(db, gradeId)).RecordedOn);
    }

    [Theory]
    [InlineData("2025-07-31")]
    [InlineData("2026-08-01")]
    public async Task UpdateGradeAsync_Throws_ForDateOutsideTheYear(string date)
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", joinedOn: new DateTime(2025, 1, 1));
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day);
        var day = DateOnly.Parse(date, CultureInfo.InvariantCulture);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).UpdateGradeAsync(gradeId, 3.0m, day));

        var stored = await LoadGradeAsync(db, gradeId);
        Assert.Equal(Day, stored.RecordedOn);
        Assert.Equal(2.0m, stored.Value);
    }

    // UpdateGradeAsync

    [Fact]
    public async Task UpdateGradeAsync_Succeeds_ForValueDateLabelAndCommentChange()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day, assessmentId);

        await CreateService(db).UpdateGradeAsync(gradeId, 3.3m, new DateOnly(2025, 9, 20), "  Retake ", "  Late  ");

        var stored = await LoadGradeAsync(db, gradeId);
        Assert.Equal(3.3m, stored.Value);
        Assert.Equal(new DateOnly(2025, 9, 20), stored.RecordedOn);
        Assert.Equal("Retake", stored.Label);
        Assert.Equal("Late", stored.Comment);
        Assert.Equal(assessmentId, stored.AssessmentId);
    }

    [Fact]
    public async Task UpdateGradeAsync_StoresNull_ForEmptyLabelAndComment()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day);

        await CreateService(db).UpdateGradeAsync(gradeId, 2.0m, Day, "   ", "");

        var stored = await LoadGradeAsync(db, gradeId);
        Assert.Null(stored.Label);
        Assert.Null(stored.Comment);
    }

    [Fact]
    public async Task UpdateGradeAsync_ChangesNothing_ForIdenticalValues()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day);
        var before = await LoadGradeAsync(db, gradeId);

        await CreateService(db).UpdateGradeAsync(gradeId, 2.0m, Day);

        var after = await LoadGradeAsync(db, gradeId);
        Assert.Equal(before.Value, after.Value);
        Assert.Equal(before.RecordedOn, after.RecordedOn);
        Assert.Equal(before.Label, after.Label);
        Assert.Equal(before.Comment, after.Comment);
        Assert.Equal(before.VoidedOn, after.VoidedOn);
        Assert.Equal(1, await CountGradesAsync(db));
    }

    [Fact]
    public async Task UpdateGradeAsync_Throws_ForMissingGrade()
    {
        using var db = new SqliteTestDatabase();
        await GradeSheetSeed.CreateAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).UpdateGradeAsync(9999, 2.0m, Day));
    }

    [Fact]
    public async Task UpdateGradeAsync_Throws_ForVoidedGrade()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day, voidedOn: Now);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).UpdateGradeAsync(gradeId, 3.0m, Day));

        Assert.Equal(2.0m, (await LoadGradeAsync(db, gradeId)).Value);
    }

    [Fact]
    public async Task UpdateGradeAsync_Throws_ForGradeOfAVoidedAssessment()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day, voidedOn: Now);
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day, assessmentId);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).UpdateGradeAsync(gradeId, 3.0m, Day));

        Assert.Equal(2.0m, (await LoadGradeAsync(db, gradeId)).Value);
    }

    [Fact]
    public async Task UpdateGradeAsync_Throws_ForIllegalGradeValue()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).UpdateGradeAsync(gradeId, 2.5m, Day));

        Assert.Equal(2.0m, (await LoadGradeAsync(db, gradeId)).Value);
    }

    [Fact]
    public async Task UpdateGradeAsync_Throws_ForNewDateBeforeEnrollment()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", joinedOn: new DateTime(2025, 9, 10));
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).UpdateGradeAsync(gradeId, 2.0m, new DateOnly(2025, 9, 9)));

        Assert.Equal(Day, (await LoadGradeAsync(db, gradeId)).RecordedOn);
    }

    [Fact]
    public async Task UpdateGradeAsync_Succeeds_ForNewDateOnTheJoinDate()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", joinedOn: new DateTime(2025, 9, 10));
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day);

        await CreateService(db).UpdateGradeAsync(gradeId, 2.0m, new DateOnly(2025, 9, 10));

        Assert.Equal(new DateOnly(2025, 9, 10), (await LoadGradeAsync(db, gradeId)).RecordedOn);
    }

    [Fact]
    public async Task UpdateGradeAsync_Throws_ForNewDateOnTheLeaveDate()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", leftOn: new DateTime(2025, 10, 1));
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).UpdateGradeAsync(gradeId, 2.0m, new DateOnly(2025, 10, 1)));

        Assert.Equal(Day, (await LoadGradeAsync(db, gradeId)).RecordedOn);
    }

    [Fact]
    public async Task UpdateGradeAsync_Throws_ForExemptStudent()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day);
        await seed.AddExemptionAsync(student);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).UpdateGradeAsync(gradeId, 3.0m, Day));

        Assert.Equal(2.0m, (await LoadGradeAsync(db, gradeId)).Value);
    }

    [Fact]
    public async Task UpdateGradeAsync_Succeeds_ForCommentOfExactlyTheMaximumLength()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day);
        var comment = new string('x', GradeService.MaxCommentLength);

        await CreateService(db).UpdateGradeAsync(gradeId, 2.0m, Day, comment: comment);

        Assert.Equal(comment, (await LoadGradeAsync(db, gradeId)).Comment);
    }

    [Fact]
    public async Task UpdateGradeAsync_Throws_ForTooLongComment()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).UpdateGradeAsync(gradeId, 2.0m, Day, comment: new string('x', GradeService.MaxCommentLength + 1)));

        Assert.Null((await LoadGradeAsync(db, gradeId)).Comment);
    }

    [Fact]
    public async Task UpdateGradeAsync_Throws_ForTooLongLabel()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).UpdateGradeAsync(gradeId, 2.0m, Day, label: new string('x', GradeService.MaxLabelLength + 1)));

        Assert.Null((await LoadGradeAsync(db, gradeId)).Label);
    }

    // VoidGradeAsync and RestoreGradeAsync

    [Fact]
    public async Task VoidGradeAsync_SetsVoidedOnFromTheClock_AndKeepsTheGrade()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day);

        await CreateService(db).VoidGradeAsync(gradeId);

        var stored = await LoadGradeAsync(db, gradeId);
        Assert.Equal(Now, stored.VoidedOn);
        Assert.Equal(2.0m, stored.Value);
    }

    [Fact]
    public async Task VoidGradeAsync_Throws_ForMissingGrade()
    {
        using var db = new SqliteTestDatabase();
        await GradeSheetSeed.CreateAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).VoidGradeAsync(9999));
    }

    [Fact]
    public async Task VoidGradeAsync_Throws_WhenCalledTwice()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day);
        var service = CreateService(db);
        await service.VoidGradeAsync(gradeId);

        await Assert.ThrowsAsync<InvariantViolationException>(() => service.VoidGradeAsync(gradeId));

        Assert.Equal(Now, (await LoadGradeAsync(db, gradeId)).VoidedOn);
    }

    [Fact]
    public async Task RestoreGradeAsync_ClearsVoidedOn_AfterVoiding()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day, assessmentId);
        var service = CreateService(db);
        await service.VoidGradeAsync(gradeId);

        await service.RestoreGradeAsync(gradeId);

        var stored = await LoadGradeAsync(db, gradeId);
        Assert.Null(stored.VoidedOn);
        Assert.Equal(2.0m, stored.Value);
    }

    [Fact]
    public async Task RestoreGradeAsync_Throws_ForGradeThatIsNotVoided()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RestoreGradeAsync(gradeId));
    }

    [Fact]
    public async Task RestoreGradeAsync_Throws_ForMissingGrade()
    {
        using var db = new SqliteTestDatabase();
        await GradeSheetSeed.CreateAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RestoreGradeAsync(9999));
    }

    [Fact]
    public async Task RestoreGradeAsync_Throws_WhenAnotherActiveGradeExistsForTheSameAssessmentAndStudent()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day);
        var voided = await seed.AddGradeAsync(student, 2.0m, Day, assessmentId, voidedOn: Now);
        await seed.AddGradeAsync(student, 3.0m, Day, assessmentId);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RestoreGradeAsync(voided));

        Assert.Equal(Now, (await LoadGradeAsync(db, voided)).VoidedOn);
    }

    [Fact]
    public async Task RestoreGradeAsync_Throws_WhenTheEnrollmentNoLongerCoversTheGradeDate()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day, voidedOn: Now);
        await using (var context = db.CreateContext())
        {
            var enrollment = await context.Enrollments.SingleAsync(e => e.StudentId == student);
            enrollment.LeftOn = new DateTime(2025, 9, 1);
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RestoreGradeAsync(gradeId));

        Assert.Equal(Now, (await LoadGradeAsync(db, gradeId)).VoidedOn);
    }

    [Fact]
    public async Task RestoreGradeAsync_Throws_WhenTheStudentIsNowExempt()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day, voidedOn: Now);
        await seed.AddExemptionAsync(student);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RestoreGradeAsync(gradeId));

        Assert.Equal(Now, (await LoadGradeAsync(db, gradeId)).VoidedOn);
    }

    [Fact]
    public async Task RestoreGradeAsync_Throws_ForGradeOfAVoidedAssessment()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessmentId = await seed.AddAssessmentAsync("Test one", Day, voidedOn: Now);
        var gradeId = await seed.AddGradeAsync(student, 2.0m, Day, assessmentId, voidedOn: Now);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RestoreGradeAsync(gradeId));

        Assert.Equal(Now, (await LoadGradeAsync(db, gradeId)).VoidedOn);
    }
}
