using Microsoft.EntityFrameworkCore;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Grading;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class GradeSheetServiceTests
{
    private static readonly DateOnly Day = new(2025, 9, 15);

    private static readonly DateTime Now = new(2025, 10, 1, 10, 0, 0);

    private static GradeSheetService CreateService(SqliteTestDatabase db, DateTime? now = null) =>
        new(db.CreateContextFactory(), new FixedClock(now ?? Now));

    private static GradeSheetStudent Student(GradeSheet sheet, int studentId) =>
        sheet.Students.Single(s => s.StudentId == studentId);

    // Header and structure

    [Fact]
    public async Task GetGradeSheetAsync_ReturnsHeaderData_ForSubjectInstance()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        Assert.Equal(seed.SubjectInstanceId, sheet.SubjectInstanceId);
        Assert.Equal("Testkunde", sheet.SubjectName);
        Assert.Equal("5A", sheet.ClassName);
        Assert.Equal(seed.YearId, sheet.SchoolYearId);
        Assert.Equal("2025/26", sheet.SchoolYearLabel);
    }

    [Fact]
    public async Task GetGradeSheetAsync_Throws_ForMissingSubjectInstance()
    {
        using var db = new SqliteTestDatabase();
        await GradeSheetSeed.CreateAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).GetGradeSheetAsync(9999));
    }

    [Fact]
    public async Task GetGradeSheetAsync_ReturnsBucketsInSortOrder_WithAssessmentsByDateThenId()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var oral = await seed.AddBucketAsync("Oral", 40m, sortOrder: 0);
        var late = await seed.AddAssessmentAsync("Late", new DateOnly(2025, 11, 1));
        var earlyFirst = await seed.AddAssessmentAsync("Early first", new DateOnly(2025, 9, 1));
        var earlySecond = await seed.AddAssessmentAsync("Early second", new DateOnly(2025, 9, 1));
        var oralOne = await seed.AddAssessmentAsync("Oral one", Day, oral);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        Assert.Equal(["Oral", "Written"], [.. sheet.Buckets.Select(b => b.Name)]);
        Assert.Equal([oralOne], [.. sheet.Buckets[0].Assessments.Select(a => a.Id)]);
        Assert.Equal([earlyFirst, earlySecond, late], [.. sheet.Buckets[1].Assessments.Select(a => a.Id)]);
    }

    [Fact]
    public async Task GetGradeSheetAsync_ReturnsNoStudents_ForSubjectWithoutEnrollments()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        await seed.AddAssessmentAsync("Test one", Day);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        Assert.Empty(sheet.Students);
    }

    // Students: sorting, left students, cell states

    [Fact]
    public async Task GetGradeSheetAsync_ListsActiveStudentsFirst_ThenStudentsWhoLeft_EachByName()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var leftAlpha = await seed.AddStudentAsync("Test", "Alpha", leftOn: new DateTime(2025, 12, 1));
        var activeZulu = await seed.AddStudentAsync("Test", "Zulu");
        var activeBeta = await seed.AddStudentAsync("Test", "Beta");
        var leftMike = await seed.AddStudentAsync("Test", "Mike", leftOn: new DateTime(2025, 12, 1));

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        Assert.Equal([activeBeta, activeZulu, leftAlpha, leftMike], [.. sheet.Students.Select(s => s.StudentId)]);
    }

    [Fact]
    public async Task GetGradeSheetAsync_ShowsAndMarksAStudentWhoLeft_EvenWithoutGrades()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", joinedOn: new DateTime(2025, 9, 1), leftOn: new DateTime(2025, 12, 1));
        var active = await seed.AddStudentAsync("Test", "Beta");

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        var left = Student(sheet, student);
        Assert.Equal(new DateOnly(2025, 9, 1), left.JoinedOn);
        Assert.Equal(new DateOnly(2025, 12, 1), left.LeftOn);
        Assert.Null(Student(sheet, active).LeftOn);
    }

    [Fact]
    public async Task GetGradeSheetAsync_MarksCellsBeforeJoiningAndOnOrAfterLeaving_AsNotEnrolled()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync(
            "Test", "Alpha", joinedOn: new DateTime(2025, 9, 10), leftOn: new DateTime(2025, 10, 1));
        var beforeJoin = await seed.AddAssessmentAsync("Before join", new DateOnly(2025, 9, 9));
        var onJoin = await seed.AddAssessmentAsync("On join", new DateOnly(2025, 9, 10));
        var lastDay = await seed.AddAssessmentAsync("Last day", new DateOnly(2025, 9, 30));
        var onLeave = await seed.AddAssessmentAsync("On leave", new DateOnly(2025, 10, 1));
        var afterLeave = await seed.AddAssessmentAsync("After leave", new DateOnly(2025, 10, 2));

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        var states = Student(sheet, student).Cells.ToDictionary(c => c.AssessmentId, c => c.State);
        Assert.Equal(GradeSheetCellState.NotEnrolled, states[beforeJoin]);
        Assert.Equal(GradeSheetCellState.Editable, states[onJoin]);
        Assert.Equal(GradeSheetCellState.Editable, states[lastDay]);
        Assert.Equal(GradeSheetCellState.NotEnrolled, states[onLeave]);
        Assert.Equal(GradeSheetCellState.NotEnrolled, states[afterLeave]);
    }

    [Fact]
    public async Task GetGradeSheetAsync_MarksTheCellEditable_OnTheJoinDay_WhenJoinedOnHasATimeOfDay()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", joinedOn: new DateTime(2025, 9, 10, 9, 15, 0));
        var beforeJoin = await seed.AddAssessmentAsync("Before join", new DateOnly(2025, 9, 9));
        var onJoin = await seed.AddAssessmentAsync("On join", new DateOnly(2025, 9, 10));

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        var states = Student(sheet, student).Cells.ToDictionary(c => c.AssessmentId, c => c.State);
        Assert.Equal(GradeSheetCellState.NotEnrolled, states[beforeJoin]);
        Assert.Equal(GradeSheetCellState.Editable, states[onJoin]);
    }

    [Fact]
    public async Task GetGradeSheetAsync_MarksTheCellNotEnrolled_OnTheLeaveDay_WhenLeftOnHasATimeOfDay()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", leftOn: new DateTime(2025, 10, 1, 14, 30, 0));
        var dayBefore = await seed.AddAssessmentAsync("Day before", new DateOnly(2025, 9, 30));
        var onLeave = await seed.AddAssessmentAsync("On leave", new DateOnly(2025, 10, 1));

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        var states = Student(sheet, student).Cells.ToDictionary(c => c.AssessmentId, c => c.State);
        Assert.Equal(GradeSheetCellState.Editable, states[dayBefore]);
        Assert.Equal(GradeSheetCellState.NotEnrolled, states[onLeave]);
    }

    [Fact]
    public async Task GetGradeSheetAsync_ReturnsOneCellPerAssessment_InColumnOrder()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var oral = await seed.AddBucketAsync("Oral", 40m, sortOrder: 2);
        var second = await seed.AddAssessmentAsync("Oral one", Day, oral);
        var first = await seed.AddAssessmentAsync("Written one", Day);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        Assert.Equal([first, second], [.. Student(sheet, student).Cells.Select(c => c.AssessmentId)]);
    }

    [Fact]
    public async Task GetGradeSheetAsync_ReturnsTheGradeTokenValueAndDetails_InTheCell()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessment = await seed.AddAssessmentAsync("Test one", Day);
        var gradeId = await seed.AddGradeAsync(student, 2.3m, Day, assessment);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        var cell = Assert.Single(Student(sheet, student).Cells);
        Assert.Equal(GradeSheetCellState.Editable, cell.State);
        Assert.NotNull(cell.Grade);
        Assert.Equal(gradeId, cell.Grade.Id);
        Assert.Equal("2-", cell.Grade.Token);
        Assert.Equal(2.3m, cell.Grade.Value);
        Assert.Equal(Day, cell.Grade.RecordedOn);
        Assert.Null(cell.Grade.VoidedOn);
    }

    [Fact]
    public async Task GetGradeSheetAsync_ReturnsAnEmptyCell_WhereThereIsNoGrade()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddAssessmentAsync("Test one", Day);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        var cell = Assert.Single(Student(sheet, student).Cells);
        Assert.Null(cell.Grade);
    }

    // Exemption

    [Fact]
    public async Task GetGradeSheetAsync_MarksAllCellsOfAnExemptStudent_AsExempt()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddExemptionAsync(student);
        await seed.AddAssessmentAsync("Test one", Day);
        await seed.AddAssessmentAsync("Test two", new DateOnly(2025, 10, 1));

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        var exempt = Student(sheet, student);
        Assert.True(exempt.IsExempt);
        Assert.All(exempt.Cells, c => Assert.Equal(GradeSheetCellState.Exempt, c.State));
    }

    [Fact]
    public async Task GetGradeSheetAsync_ReturnsNoAverages_ForAnExemptStudentWithEarlierGrades()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessment = await seed.AddAssessmentAsync("Test one", Day);
        await seed.AddGradeAsync(student, 2.0m, Day, assessment);
        await seed.AddGradeAsync(student, 3.0m, Day);
        await seed.AddExemptionAsync(student);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        var exempt = Student(sheet, student);
        Assert.Null(exempt.SubjectAverage);
        Assert.Null(exempt.BucketAverages[seed.BucketId]);
        Assert.All(exempt.Cells, c => Assert.Equal(GradeSheetCellState.Exempt, c.State));
    }

    [Fact]
    public async Task GetGradeSheetAsync_DoesNotMarkOtherStudentsExempt()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var exempt = await seed.AddStudentAsync("Test", "Alpha");
        var other = await seed.AddStudentAsync("Test", "Beta");
        await seed.AddExemptionAsync(exempt);
        await seed.AddAssessmentAsync("Test one", Day);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        Assert.False(Student(sheet, other).IsExempt);
        Assert.Equal(GradeSheetCellState.Editable, Student(sheet, other).Cells[0].State);
    }

    // Unassigned grades

    [Fact]
    public async Task GetGradeSheetAsync_ListsGradesWithoutAnAssessment_AsUnassigned_AndCountsThem()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessment = await seed.AddAssessmentAsync("Test one", Day);
        await seed.AddGradeAsync(student, 2.0m, Day, assessment);
        var later = await seed.AddGradeAsync(student, 4.0m, new DateOnly(2025, 9, 20));
        var earlier = await seed.AddGradeAsync(student, 3.0m, new DateOnly(2025, 9, 18));

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        var row = Student(sheet, student);
        Assert.Equal([earlier, later], [.. row.UnassignedGrades.Select(g => g.Id)]);
        Assert.Equal(3.0m, row.BucketAverages[seed.BucketId]);
    }

    // Averages

    [Fact]
    public async Task GetGradeSheetAsync_ReturnsNullAverages_ForAnEmptyBucketAndSubject()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddAssessmentAsync("Test one", Day);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        var row = Student(sheet, student);
        Assert.Null(row.BucketAverages[seed.BucketId]);
        Assert.Null(row.SubjectAverage);
    }

    [Fact]
    public async Task GetGradeSheetAsync_ReturnsTheExactDecimalAverage_WithoutRounding()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 1.0m, Day);
        await seed.AddGradeAsync(student, 1.3m, Day);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        var row = Student(sheet, student);
        Assert.Equal(1.15m, row.BucketAverages[seed.BucketId]);
        Assert.Equal(1.15m, row.SubjectAverage);
    }

    [Fact]
    public async Task GetGradeSheetAsync_NormalisesWeights_ThatDoNotSumToHundred()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db, bucketWeight: 30m);
        var oral = await seed.AddBucketAsync("Oral", 30m, sortOrder: 2);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 2.0m, Day);
        await seed.AddGradeAsync(student, 4.0m, Day, bucketId: oral);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        Assert.Equal(3.0m, Student(sheet, student).SubjectAverage);
    }

    [Fact]
    public async Task GetGradeSheetAsync_LeavesAnEmptyBucketOutOfTheSubjectAverage()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db, bucketWeight: 60m);
        var oral = await seed.AddBucketAsync("Oral", 40m, sortOrder: 2);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 2.0m, Day);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        var row = Student(sheet, student);
        Assert.Null(row.BucketAverages[oral]);
        Assert.Equal(2.0m, row.SubjectAverage);
    }

    [Fact]
    public async Task GetGradeSheetAsync_UsesTheSnapshottedBucketWeights_AfterATemplateWeightEdit()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db, bucketWeight: 75m);
        var oral = await seed.AddBucketAsync("Oral", 25m, sortOrder: 2);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 2.0m, Day);
        await seed.AddGradeAsync(student, 4.0m, Day, bucketId: oral);
        await using (var context = db.CreateContext())
        {
            var subjectId = (await context.SubjectInstances.SingleAsync(si => si.Id == seed.SubjectInstanceId)).SubjectId;
            context.BucketTemplates.AddRange(
                new BucketTemplate { SubjectId = subjectId, Name = "Written", Weight = 10m, SortOrder = 1 },
                new BucketTemplate { SubjectId = subjectId, Name = "Oral", Weight = 90m, SortOrder = 2 });
            await context.SaveChangesAsync();
        }

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        // 2.0 * 0.75 + 4.0 * 0.25 = 2.5 with the snapshotted weights (the templates would give 3.8).
        Assert.Equal(2.5m, Student(sheet, student).SubjectAverage);
    }

    // Voided grades and assessments

    [Fact]
    public async Task GetGradeSheetAsync_OmitsVoidedGrades_ByDefault()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessment = await seed.AddAssessmentAsync("Test one", Day);
        await seed.AddGradeAsync(student, 6.0m, Day, assessment, voidedOn: Now);
        await seed.AddGradeAsync(student, 6.0m, Day, voidedOn: Now);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        var row = Student(sheet, student);
        Assert.Null(Assert.Single(row.Cells).Grade);
        Assert.Empty(row.UnassignedGrades);
        Assert.Null(row.SubjectAverage);
    }

    [Fact]
    public async Task GetGradeSheetAsync_ShowsVoidedGradesFlagged_WhenIncludeVoided_ButNeverAveragesThem()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessment = await seed.AddAssessmentAsync("Test one", Day);
        var other = await seed.AddAssessmentAsync("Test two", Day);
        await seed.AddGradeAsync(student, 6.0m, Day, assessment, voidedOn: Now);
        await seed.AddGradeAsync(student, 2.0m, Day, other);
        await seed.AddGradeAsync(student, 6.0m, Day, voidedOn: Now);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId, includeVoided: true);

        var row = Student(sheet, student);
        Assert.Equal(Now, row.Cells[0].Grade!.VoidedOn);
        Assert.Null(row.Cells[1].Grade!.VoidedOn);
        Assert.Equal(Now, Assert.Single(row.UnassignedGrades).VoidedOn);
        Assert.Equal(2.0m, row.SubjectAverage);
    }

    [Fact]
    public async Task GetGradeSheetAsync_ShowsTheActiveGradeInTheCell_WhenAVoidedOneSitsNextToIt()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessment = await seed.AddAssessmentAsync("Test one", Day);
        await seed.AddGradeAsync(student, 6.0m, Day, assessment, voidedOn: Now);
        var active = await seed.AddGradeAsync(student, 2.0m, Day, assessment);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId, includeVoided: true);

        var cell = Assert.Single(Student(sheet, student).Cells);
        Assert.Equal(active, cell.Grade!.Id);
        Assert.Null(cell.Grade.VoidedOn);
    }

    [Fact]
    public async Task GetGradeSheetAsync_OmitsAVoidedAssessmentAndItsGrades_ByDefault()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var active = await seed.AddAssessmentAsync("Test one", Day);
        var voided = await seed.AddAssessmentAsync("Test two", Day, voidedOn: Now);
        await seed.AddGradeAsync(student, 2.0m, Day, active);
        await seed.AddGradeAsync(student, 6.0m, Day, voided);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        Assert.Equal([active], [.. sheet.Buckets[0].Assessments.Select(a => a.Id)]);
        var row = Student(sheet, student);
        Assert.Equal(active, Assert.Single(row.Cells).AssessmentId);
        Assert.Equal(2.0m, row.SubjectAverage);
    }

    [Fact]
    public async Task GetGradeSheetAsync_ShowsAVoidedAssessmentFlagged_WhenIncludeVoided_ButNeverAveragesItsGrades()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var active = await seed.AddAssessmentAsync("Test one", Day);
        var voided = await seed.AddAssessmentAsync("Test two", new DateOnly(2025, 9, 16), voidedOn: Now);
        await seed.AddGradeAsync(student, 2.0m, Day, active);
        await seed.AddGradeAsync(student, 6.0m, new DateOnly(2025, 9, 16), voided);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId, includeVoided: true);

        var assessments = sheet.Buckets[0].Assessments;
        Assert.Equal([active, voided], [.. assessments.Select(a => a.Id)]);
        Assert.Null(assessments[0].VoidedOn);
        Assert.Equal(Now, assessments[1].VoidedOn);
        var row = Student(sheet, student);
        Assert.NotNull(row.Cells[1].Grade!.VoidedOn);
        Assert.Equal(2.0m, row.SubjectAverage);
    }

    // Lock state

    [Fact]
    public async Task GetGradeSheetAsync_IsWritableAndNotClosed_ForAnActiveYear()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        Assert.True(sheet.IsWritable);
        Assert.False(sheet.IsClosed);
        Assert.Null(sheet.UnlockedUntil);
    }

    [Fact]
    public async Task GetGradeSheetAsync_IsNotWritableAndClosed_ForALockedClosedYear()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db, SchoolYearStatus.Closed);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        Assert.False(sheet.IsWritable);
        Assert.True(sheet.IsClosed);
    }

    [Fact]
    public async Task GetGradeSheetAsync_IsWritableButStillClosed_ForAnUnlockedClosedYear()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var until = Now.AddHours(1);
        await seed.SetYearStatusAsync(SchoolYearStatus.Closed, until, "Corrected a transcription error");

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        Assert.True(sheet.IsWritable);
        Assert.True(sheet.IsClosed);
        Assert.Equal(until, sheet.UnlockedUntil);
    }

    [Fact]
    public async Task GetGradeSheetAsync_IsNotWritable_OnceTheUnlockWindowHasExpired()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        await seed.SetYearStatusAsync(SchoolYearStatus.Closed, Now.AddHours(1), "Corrected a transcription error");

        var sheet = await CreateService(db, now: Now.AddHours(2)).GetGradeSheetAsync(seed.SubjectInstanceId);

        Assert.False(sheet.IsWritable);
        Assert.True(sheet.IsClosed);
    }

    [Fact]
    public async Task GetGradeSheetAsync_IsWritable_ForAPlannedYear()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db, SchoolYearStatus.Planned);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        Assert.True(sheet.IsWritable);
        Assert.False(sheet.IsClosed);
    }

    // Final grade

    private static async Task AddOverrideAsync(SqliteTestDatabase db, GradeSheetSeed seed, int studentId, decimal? value)
    {
        await using var context = db.CreateContext();
        context.FinalGrades.Add(new FinalGrade { SubjectInstanceId = seed.SubjectInstanceId, StudentId = studentId, OverrideValue = value });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetGradeSheetAsync_ReturnsOverrideAndProposal_ForStudentWithBoth()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 2.0m, Day);
        await AddOverrideAsync(db, seed, student, 4m);

        var row = Student(await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId), student);

        Assert.Equal(4m, row.FinalGradeOverride);
        Assert.Equal(2m, row.ProposedFinalGrade);
        Assert.Equal(4m, row.FinalGrade);
    }

    [Fact]
    public async Task GetGradeSheetAsync_ReturnsProposalAsFinalGrade_WithoutOverride()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 3.0m, Day);

        var row = Student(await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId), student);

        Assert.Null(row.FinalGradeOverride);
        Assert.Equal(3m, row.FinalGrade);
    }

    [Fact]
    public async Task GetGradeSheetAsync_ReturnsNullProposal_ForStudentWithoutGrades()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");

        var row = Student(await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId), student);

        Assert.Null(row.ProposedFinalGrade);
        Assert.Null(row.FinalGrade);
    }

    [Fact]
    public async Task GetGradeSheetAsync_IgnoresVoidedGrades_ForProposal()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 2.0m, Day);
        await seed.AddGradeAsync(student, 6.0m, Day, voidedOn: Now);

        var row = Student(await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId, includeVoided: true), student);

        Assert.Equal(2m, row.ProposedFinalGrade);
    }

    [Fact]
    public async Task GetGradeSheetAsync_IgnoresGradesOfVoidedAssessments_ForProposal()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 2.0m, Day);
        var voidedAssessment = await seed.AddAssessmentAsync("Test one", Day, voidedOn: Now);
        await seed.AddGradeAsync(student, 6.0m, Day, voidedAssessment);

        var row = Student(await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId, includeVoided: true), student);

        Assert.Equal(2m, row.ProposedFinalGrade);
    }

    [Fact]
    public async Task GetGradeSheetAsync_ReturnsNoProposalAndNoOverride_ForExemptStudentWithStoredOverride()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 2.0m, Day);
        await AddOverrideAsync(db, seed, student, 4m);
        await seed.AddExemptionAsync(student);

        var row = Student(await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId), student);

        Assert.Null(row.ProposedFinalGrade);
        Assert.Null(row.FinalGradeOverride);
        Assert.Null(row.FinalGrade);
    }

    [Fact]
    public async Task GetGradeSheetAsync_ReturnsNullOverride_ForClearedOverride()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await AddOverrideAsync(db, seed, student, null);

        var row = Student(await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId), student);

        Assert.Null(row.FinalGradeOverride);
    }

    [Fact]
    public async Task GetGradeSheetAsync_ReturnsOverride_ForClosedYear()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await AddOverrideAsync(db, seed, student, 5m);
        await seed.SetYearStatusAsync(SchoolYearStatus.Closed);

        var sheet = await CreateService(db).GetGradeSheetAsync(seed.SubjectInstanceId);

        Assert.Equal(5m, Student(sheet, student).FinalGradeOverride);
    }
}
