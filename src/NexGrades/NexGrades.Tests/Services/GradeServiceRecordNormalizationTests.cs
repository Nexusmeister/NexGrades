using Microsoft.EntityFrameworkCore;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;
using NexGrades.Tests.TestSupport;

namespace NexGrades.Tests.Services;

public class GradeServiceRecordNormalizationTests
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

    [Fact]
    public async Task RecordGradeAsync_TrimsLabelAndComment_BeforeStoring()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");

        var grade = await CreateService(db).RecordGradeAsync(seed.BucketId, student, 2.0m, Day, "  Quiz  ", "\tWell done \n");

        var stored = await LoadGradeAsync(db, grade.Id);
        Assert.Equal("Quiz", stored.Label);
        Assert.Equal("Well done", stored.Comment);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RecordGradeAsync_StoresNull_ForBlankLabelAndComment(string? blank)
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");

        var grade = await CreateService(db).RecordGradeAsync(seed.BucketId, student, 2.0m, Day, blank, blank);

        var stored = await LoadGradeAsync(db, grade.Id);
        Assert.Null(stored.Label);
        Assert.Null(stored.Comment);
    }

    [Fact]
    public async Task RecordGradeAsync_Succeeds_ForLabelAndCommentOfExactlyTheMaximumLength()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");

        var grade = await CreateService(db).RecordGradeAsync(
            seed.BucketId,
            student,
            2.0m,
            Day,
            new string('a', GradeService.MaxLabelLength),
            new string('b', GradeService.MaxCommentLength));

        var stored = await LoadGradeAsync(db, grade.Id);
        Assert.Equal(GradeService.MaxLabelLength, stored.Label!.Length);
        Assert.Equal(GradeService.MaxCommentLength, stored.Comment!.Length);
    }

    [Fact]
    public async Task RecordGradeAsync_Succeeds_WhenOnlyPaddingPushesTheLabelOverTheMaximum()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var padded = "  " + new string('a', GradeService.MaxLabelLength) + "  ";

        var grade = await CreateService(db).RecordGradeAsync(seed.BucketId, student, 2.0m, Day, padded);

        Assert.Equal(GradeService.MaxLabelLength, (await LoadGradeAsync(db, grade.Id)).Label!.Length);
    }

    [Fact]
    public async Task RecordGradeAsync_Throws_ForTooLongLabel_AndStoresNothing()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RecordGradeAsync(
            seed.BucketId, student, 2.0m, Day, new string('a', GradeService.MaxLabelLength + 1)));

        Assert.Equal(0, await CountGradesAsync(db));
    }

    [Fact]
    public async Task RecordGradeAsync_Throws_ForTooLongComment_AndStoresNothing()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RecordGradeAsync(
            seed.BucketId, student, 2.0m, Day, comment: new string('b', GradeService.MaxCommentLength + 1)));

        Assert.Equal(0, await CountGradesAsync(db));
    }
}
