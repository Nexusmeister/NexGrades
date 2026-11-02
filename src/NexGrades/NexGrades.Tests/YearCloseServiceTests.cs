using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.Data.Entities;
using NexGrades.Domain.Auditing;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class YearCloseServiceTests
{
    [Fact]
    public async Task GetCloseReadinessAsync_Blocks_WhenAClassInstanceIsStillActive()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var year = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A", Status = ClassInstanceStatus.Active };
        context.AddRange(year, group, instance);
        await context.SaveChangesAsync();

        var service = new YearCloseService(db.CreateContextFactory(), new FixedClock(DateTime.Now), NullLogger<YearCloseService>.Instance);
        var readiness = await service.GetCloseReadinessAsync(year.Id);

        Assert.False(readiness.CanClose);
        Assert.Contains(readiness.BlockingIssues, issue => issue.Kind == CloseReadinessIssueKind.ClassStillActive && issue.ClassName == "5A");
    }

    [Fact]
    public async Task GetCloseReadinessAsync_Warns_ButDoesNotBlock_WhenBucketWeightsDoNotSumTo100()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var year = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A", Status = ClassInstanceStatus.RolledForward };
        var subject = new Subject { Name = "Mathematik", ShortCode = "MA" };
        var subjectInstance = new SubjectInstance { ClassInstance = instance, Subject = subject };
        var bucket = new GradeBucket { SubjectInstance = subjectInstance, Name = "Schriftlich", Weight = 90m };
        context.AddRange(year, group, instance, subject, subjectInstance, bucket);
        await context.SaveChangesAsync();

        var service = new YearCloseService(db.CreateContextFactory(), new FixedClock(DateTime.Now), NullLogger<YearCloseService>.Instance);
        var readiness = await service.GetCloseReadinessAsync(year.Id);

        Assert.True(readiness.CanClose);
        Assert.Contains(readiness.Warnings, w => w.Kind == CloseReadinessIssueKind.BucketWeightsNotHundred && w.WeightTotal == 90m);
    }

    [Fact]
    public async Task CloseSchoolYearAsync_SetsClosedStatusAndTimestamp_OnceEveryClassIsDispositioned()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var year = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A", Status = ClassInstanceStatus.Archived };
        context.AddRange(year, group, instance);
        await context.SaveChangesAsync();

        var now = new DateTime(2026, 7, 20);
        var service = new YearCloseService(db.CreateContextFactory(), new FixedClock(now), NullLogger<YearCloseService>.Instance);
        await service.CloseSchoolYearAsync(year.Id);

        await using var verify = db.CreateContext();
        var refreshed = await verify.SchoolYears.FindAsync(year.Id);
        Assert.NotNull(refreshed);
        Assert.Equal(SchoolYearStatus.Closed, refreshed!.Status);
        Assert.Equal(now, refreshed.ClosedOn);
    }

    [Fact]
    public async Task CloseSchoolYearAsync_Throws_WhenBlockingIssuesRemain()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var year = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A", Status = ClassInstanceStatus.Active };
        context.AddRange(year, group, instance);
        await context.SaveChangesAsync();

        var service = new YearCloseService(db.CreateContextFactory(), new FixedClock(DateTime.Now), NullLogger<YearCloseService>.Instance);

        await Assert.ThrowsAsync<Domain.Exceptions.InvariantViolationException>(() => service.CloseSchoolYearAsync(year.Id));
    }

    private static readonly DateTime Now = new(2026, 7, 20, 10, 0, 0);

    private sealed record Seeded(int OldYearId, int NextYearId, int ClassId);

    private static YearCloseService CreateService(SqliteTestDatabase db) =>
        new(db.CreateContextFactory(), new FixedClock(Now), NullLogger<YearCloseService>.Instance);

    private static async Task<Seeded> SeedAsync(
        SqliteTestDatabase db,
        ClassInstanceStatus classStatus = ClassInstanceStatus.RolledForward,
        SchoolYearStatus oldStatus = SchoolYearStatus.Active,
        SchoolYearStatus nextStatus = SchoolYearStatus.Planned)
    {
        await using var context = db.CreateContext();
        var oldYear = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31), Status = oldStatus };
        var nextYear = new SchoolYear { Label = "2026/27", StartDate = new DateTime(2026, 8, 1), EndDate = new DateTime(2027, 7, 31), Status = nextStatus };
        var group = new ClassGroup { CohortLabel = "Cohort A", FoundedInSchoolYear = oldYear };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = oldYear, Name = "5A", Status = classStatus };
        context.AddRange(oldYear, nextYear, group, instance);
        await context.SaveChangesAsync();
        return new Seeded(oldYear.Id, nextYear.Id, instance.Id);
    }

    private static async Task<(int SubjectInstanceId, int BucketId)> AddSubjectAsync(
        SqliteTestDatabase db, int classId, string subjectName, decimal bucketWeight)
    {
        await using var context = db.CreateContext();
        var subject = new Subject { Name = subjectName, ShortCode = "SA" };
        var subjectInstance = new SubjectInstance { ClassInstanceId = classId, Subject = subject };
        var bucket = new GradeBucket { SubjectInstance = subjectInstance, Name = "Bucket A", Weight = bucketWeight };
        context.AddRange(subject, subjectInstance, bucket);
        await context.SaveChangesAsync();
        return (subjectInstance.Id, bucket.Id);
    }

    private static async Task<int> AddStudentAsync(SqliteTestDatabase db, int classId, string firstName, string lastName)
    {
        await using var context = db.CreateContext();
        var student = new Student { FirstName = firstName, LastName = lastName };
        context.Students.Add(student);
        context.Enrollments.Add(new Enrollment { Student = student, ClassInstanceId = classId, JoinedOn = new DateTime(2025, 8, 1) });
        await context.SaveChangesAsync();
        return student.Id;
    }

    private static async Task AddGradeAsync(SqliteTestDatabase db, int bucketId, int studentId)
    {
        await using var context = db.CreateContext();
        context.Grades.Add(new Grade { GradeBucketId = bucketId, StudentId = studentId, Value = 2.0m, RecordedOn = new DateOnly(2026, 3, 1) });
        await context.SaveChangesAsync();
    }

    private static async Task<SchoolYear> LoadYearAsync(SqliteTestDatabase db, int id)
    {
        await using var context = db.CreateContext();
        return await context.SchoolYears.AsNoTracking().SingleAsync(sy => sy.Id == id);
    }

    [Fact]
    public async Task GetCloseReadinessAsync_ReportsClassName_ForActiveClass()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db, ClassInstanceStatus.Active);

        var readiness = await CreateService(db).GetCloseReadinessAsync(seeded.OldYearId);

        var issue = Assert.Single(readiness.BlockingIssues);
        Assert.Equal(CloseReadinessIssueKind.ClassStillActive, issue.Kind);
        Assert.Equal(seeded.ClassId, issue.ClassInstanceId);
        Assert.Equal("5A", issue.ClassName);
    }

    [Fact]
    public async Task GetCloseReadinessAsync_Throws_ForMissingYear()
    {
        using var db = new SqliteTestDatabase();

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).GetCloseReadinessAsync(4711));
    }

    [Fact]
    public async Task GetCloseReadinessAsync_WarnsWithSubjectName_WhenSubjectHasNoGrades()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db);
        await AddSubjectAsync(db, seeded.ClassId, "Subject A", 100m);

        var readiness = await CreateService(db).GetCloseReadinessAsync(seeded.OldYearId);

        Assert.True(readiness.CanClose);
        var warning = Assert.Single(readiness.Warnings);
        Assert.Equal(CloseReadinessIssueKind.SubjectWithoutGrades, warning.Kind);
        Assert.Equal("5A", warning.ClassName);
        Assert.Equal("Subject A", warning.SubjectName);
    }

    [Fact]
    public async Task GetCloseReadinessAsync_ReportsWeightTotal_WhenBucketWeightsAreNinety()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db);
        var (_, bucketId) = await AddSubjectAsync(db, seeded.ClassId, "Subject A", 90m);
        var studentId = await AddStudentAsync(db, seeded.ClassId, "Student", "A");
        await AddGradeAsync(db, bucketId, studentId);

        var readiness = await CreateService(db).GetCloseReadinessAsync(seeded.OldYearId);

        var warning = Assert.Single(readiness.Warnings);
        Assert.Equal(CloseReadinessIssueKind.BucketWeightsNotHundred, warning.Kind);
        Assert.Equal(90m, warning.WeightTotal);
        Assert.Equal("Subject A", warning.SubjectName);
    }

    [Fact]
    public async Task GetCloseReadinessAsync_DoesNotWarnAboutWeights_WhenBucketWeightsAreHundred()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db);
        await AddSubjectAsync(db, seeded.ClassId, "Subject A", 100m);

        var readiness = await CreateService(db).GetCloseReadinessAsync(seeded.OldYearId);

        Assert.DoesNotContain(readiness.Warnings, w => w.Kind == CloseReadinessIssueKind.BucketWeightsNotHundred);
    }

    [Fact]
    public async Task GetCloseReadinessAsync_WarnsWithStudentNames_WhenParticipatingStudentHasNoGrade()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db);
        var (_, bucketId) = await AddSubjectAsync(db, seeded.ClassId, "Subject A", 100m);
        var graded = await AddStudentAsync(db, seeded.ClassId, "Student", "B");
        var ungraded = await AddStudentAsync(db, seeded.ClassId, "Student", "A");
        await AddGradeAsync(db, bucketId, graded);

        var readiness = await CreateService(db).GetCloseReadinessAsync(seeded.OldYearId);

        var warning = Assert.Single(readiness.Warnings);
        Assert.Equal(CloseReadinessIssueKind.StudentWithoutGrade, warning.Kind);
        Assert.Equal(ungraded, warning.StudentId);
        Assert.Equal("Student", warning.StudentFirstName);
        Assert.Equal("A", warning.StudentLastName);
        Assert.Equal("Subject A", warning.SubjectName);
        Assert.Equal("5A", warning.ClassName);
    }

    [Fact]
    public async Task GetCloseReadinessAsync_DoesNotWarnAboutStudent_WhenStudentIsExempt()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db);
        var (subjectInstanceId, bucketId) = await AddSubjectAsync(db, seeded.ClassId, "Subject A", 100m);
        var graded = await AddStudentAsync(db, seeded.ClassId, "Student", "B");
        var exempt = await AddStudentAsync(db, seeded.ClassId, "Student", "A");
        await AddGradeAsync(db, bucketId, graded);
        await using (var context = db.CreateContext())
        {
            context.SubjectParticipations.Add(new SubjectParticipation
            {
                SubjectInstanceId = subjectInstanceId,
                StudentId = exempt,
                Mode = ParticipationMode.Exempt,
            });
            await context.SaveChangesAsync();
        }

        var readiness = await CreateService(db).GetCloseReadinessAsync(seeded.OldYearId);

        Assert.Empty(readiness.Warnings);
    }

    [Fact]
    public async Task GetCloseReadinessAsync_DoesNotWarnAboutStudent_WhenEnrollmentHasEnded()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db);
        var (_, bucketId) = await AddSubjectAsync(db, seeded.ClassId, "Subject A", 100m);
        var graded = await AddStudentAsync(db, seeded.ClassId, "Student", "B");
        var left = await AddStudentAsync(db, seeded.ClassId, "Student", "A");
        await AddGradeAsync(db, bucketId, graded);
        await using (var context = db.CreateContext())
        {
            var enrollment = await context.Enrollments.SingleAsync(e => e.StudentId == left);
            enrollment.LeftOn = new DateTime(2026, 1, 31);
            await context.SaveChangesAsync();
        }

        var readiness = await CreateService(db).GetCloseReadinessAsync(seeded.OldYearId);

        Assert.Empty(readiness.Warnings);
    }

    [Fact]
    public async Task GetCloseReadinessAsync_ReportsNothing_ForCompleteClass()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db);
        var (_, bucketId) = await AddSubjectAsync(db, seeded.ClassId, "Subject A", 100m);
        var studentId = await AddStudentAsync(db, seeded.ClassId, "Student", "A");
        await AddGradeAsync(db, bucketId, studentId);

        var readiness = await CreateService(db).GetCloseReadinessAsync(seeded.OldYearId);

        Assert.True(readiness.CanClose);
        Assert.Empty(readiness.BlockingIssues);
        Assert.Empty(readiness.Warnings);
    }

    [Fact]
    public async Task CloseAndActivateNextYearAsync_ClosesOldYearAndActivatesNext()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db);

        await CreateService(db).CloseAndActivateNextYearAsync(seeded.OldYearId, seeded.NextYearId);

        var old = await LoadYearAsync(db, seeded.OldYearId);
        var next = await LoadYearAsync(db, seeded.NextYearId);
        Assert.Equal(SchoolYearStatus.Closed, old.Status);
        Assert.Equal(Now, old.ClosedOn);
        Assert.Equal(SchoolYearStatus.Active, next.Status);
        await using var context = db.CreateContext();
        Assert.Equal(1, await context.SchoolYears.CountAsync(sy => sy.Status == SchoolYearStatus.Active));
    }

    [Fact]
    public async Task CloseAndActivateNextYearAsync_Throws_AndChangesNothing_WhenClassIsStillActive()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db, ClassInstanceStatus.Active);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).CloseAndActivateNextYearAsync(seeded.OldYearId, seeded.NextYearId));

        var old = await LoadYearAsync(db, seeded.OldYearId);
        var next = await LoadYearAsync(db, seeded.NextYearId);
        Assert.Equal(SchoolYearStatus.Active, old.Status);
        Assert.Null(old.ClosedOn);
        Assert.Equal(SchoolYearStatus.Planned, next.Status);
    }

    [Fact]
    public async Task CloseAndActivateNextYearAsync_Throws_WhenCalledAgainAfterSuccess()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db);
        var service = CreateService(db);
        await service.CloseAndActivateNextYearAsync(seeded.OldYearId, seeded.NextYearId);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => service.CloseAndActivateNextYearAsync(seeded.OldYearId, seeded.NextYearId));

        var next = await LoadYearAsync(db, seeded.NextYearId);
        Assert.Equal(SchoolYearStatus.Active, next.Status);
    }

    [Theory]
    [InlineData(SchoolYearStatus.Planned)]
    [InlineData(SchoolYearStatus.Closed)]
    public async Task CloseAndActivateNextYearAsync_Throws_WhenOldYearIsNotActive(SchoolYearStatus oldStatus)
    {
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db, oldStatus: oldStatus);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).CloseAndActivateNextYearAsync(seeded.OldYearId, seeded.NextYearId));

        var next = await LoadYearAsync(db, seeded.NextYearId);
        Assert.Equal(SchoolYearStatus.Planned, next.Status);
    }

    [Theory]
    [InlineData(SchoolYearStatus.Active)]
    [InlineData(SchoolYearStatus.Closed)]
    public async Task CloseAndActivateNextYearAsync_Throws_WhenNextYearIsNotPlanned(SchoolYearStatus nextStatus)
    {
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db, nextStatus: nextStatus);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).CloseAndActivateNextYearAsync(seeded.OldYearId, seeded.NextYearId));

        var old = await LoadYearAsync(db, seeded.OldYearId);
        Assert.Equal(SchoolYearStatus.Active, old.Status);
    }

    [Fact]
    public async Task CloseAndActivateNextYearAsync_Throws_WhenBothIdsAreTheSameYear()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).CloseAndActivateNextYearAsync(seeded.OldYearId, seeded.OldYearId));

        var old = await LoadYearAsync(db, seeded.OldYearId);
        Assert.Equal(SchoolYearStatus.Active, old.Status);
    }

    [Fact]
    public async Task CloseAndActivateNextYearAsync_Throws_WhenNextYearIsMissing()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).CloseAndActivateNextYearAsync(seeded.OldYearId, 4711));

        var old = await LoadYearAsync(db, seeded.OldYearId);
        Assert.Equal(SchoolYearStatus.Active, old.Status);
    }

    [Fact]
    public async Task CloseAndActivateNextYearAsync_Throws_WhenNextYearDoesNotStartAfterOldYear()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db);
        await using (var context = db.CreateContext())
        {
            var next = await context.SchoolYears.SingleAsync(sy => sy.Id == seeded.NextYearId);
            next.StartDate = new DateTime(2024, 8, 1);
            next.EndDate = new DateTime(2025, 7, 31);
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).CloseAndActivateNextYearAsync(seeded.OldYearId, seeded.NextYearId));

        var old = await LoadYearAsync(db, seeded.OldYearId);
        Assert.Equal(SchoolYearStatus.Active, old.Status);
    }

    [Fact]
    public async Task CloseAndActivateNextYearAsync_LocksOldYear_AgainstLaterWrites()
    {
        var clock = new FixedClock(Now);
        using var db = new SqliteTestDatabase(new ClosedYearAuditInterceptor(clock, new UnlockReasonStore()));
        var seeded = await SeedAsync(db);
        var service = new YearCloseService(db.CreateContextFactory(), clock, NullLogger<YearCloseService>.Instance);
        await service.CloseAndActivateNextYearAsync(seeded.OldYearId, seeded.NextYearId);

        await using var context = db.CreateContext();
        var classInstance = await context.ClassInstances.SingleAsync(ci => ci.Id == seeded.ClassId);
        classInstance.Name = "5B";

        await Assert.ThrowsAsync<ClosedYearException>(() => context.SaveChangesAsync());

        await using var verify = db.CreateContext();
        Assert.Equal("5A", (await verify.ClassInstances.AsNoTracking().SingleAsync(ci => ci.Id == seeded.ClassId)).Name);
    }

    [Fact]
    public async Task CloseSchoolYearAsync_Throws_ForActiveYear_EvenWhenNothingBlocks()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).CloseSchoolYearAsync(seeded.OldYearId));

        var old = await LoadYearAsync(db, seeded.OldYearId);
        Assert.Equal(SchoolYearStatus.Active, old.Status);
        Assert.Null(old.ClosedOn);
    }
}
