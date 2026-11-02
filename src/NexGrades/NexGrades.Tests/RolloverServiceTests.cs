using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class RolloverServiceTests
{
    [Fact]
    public async Task RollClassInstanceForwardAsync_MarksOldInstanceRolledForward_AndCreatesNewOne()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var yearOld = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var yearNew = new SchoolYear { Label = "2026/27", StartDate = new DateTime(2026, 8, 1), EndDate = new DateTime(2027, 7, 31) };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = yearOld };
        var oldInstance = new ClassInstance { ClassGroup = group, SchoolYear = yearOld, Name = "5A" };
        var student = new Student { FirstName = "Anna", LastName = "Schmidt" };
        var enrollment = new Enrollment { Student = student, ClassInstance = oldInstance, JoinedOn = yearOld.StartDate };
        context.AddRange(yearOld, yearNew, group, oldInstance, student, enrollment);
        await context.SaveChangesAsync();

        var clock = new FixedClock(new DateTime(2026, 7, 15));
        var service = new RolloverService(db.CreateContextFactory(), clock);
        var newInstance = await service.RollClassInstanceForwardAsync(oldInstance.Id, yearNew.Id, "6A");

        await using var verify = db.CreateContext();
        var refreshedOld = await verify.ClassInstances.FirstAsync(ci => ci.Id == oldInstance.Id);
        Assert.Equal(ClassInstanceStatus.RolledForward, refreshedOld.Status);

        Assert.Equal("6A", newInstance.Name);
        Assert.Equal(group.Id, newInstance.ClassGroupId);
        Assert.Equal(yearNew.Id, newInstance.SchoolYearId);
        Assert.Equal(ClassInstanceStatus.Active, newInstance.Status);

        var newEnrollments = await verify.Enrollments.Where(e => e.ClassInstanceId == newInstance.Id).ToListAsync();
        Assert.Single(newEnrollments);
        Assert.Equal(student.Id, newEnrollments[0].StudentId);
    }

    [Fact]
    public async Task RollClassInstanceForwardAsync_CopiesBucketsFromCurrentTemplate_NotFromOldSnapshot()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var yearOld = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var yearNew = new SchoolYear { Label = "2026/27", StartDate = new DateTime(2026, 8, 1), EndDate = new DateTime(2027, 7, 31) };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = yearOld };
        var oldInstance = new ClassInstance { ClassGroup = group, SchoolYear = yearOld, Name = "5A" };
        var subject = new Subject { Name = "Mathematik", ShortCode = "MA" };
        var template = new BucketTemplate { Subject = subject, Name = "Schriftlich", Weight = 60m };
        var subjectInstance = new SubjectInstance { ClassInstance = oldInstance, Subject = subject };
        // Old snapshot deliberately diverges from the (later-edited) template, to prove rollover uses the template.
        var oldBucket = new GradeBucket { SubjectInstance = subjectInstance, Name = "Schriftlich", Weight = 50m };
        context.AddRange(yearOld, yearNew, group, oldInstance, subject, template, subjectInstance, oldBucket);
        await context.SaveChangesAsync();

        // Template is edited after the old instance's snapshot was taken.
        template.Weight = 80m;
        await context.SaveChangesAsync();

        var clock = new FixedClock(new DateTime(2026, 7, 15));
        var service = new RolloverService(db.CreateContextFactory(), clock);
        var newInstance = await service.RollClassInstanceForwardAsync(oldInstance.Id, yearNew.Id, "6A");

        await using var verify = db.CreateContext();
        var newBuckets = await verify.GradeBuckets
            .Where(gb => gb.SubjectInstance.ClassInstanceId == newInstance.Id)
            .ToListAsync();

        Assert.Single(newBuckets);
        Assert.Equal(80m, newBuckets[0].Weight); // current template weight, not the old 50m snapshot.

        var refreshedOldBucket = await verify.GradeBuckets.FirstAsync(gb => gb.Id == oldBucket.Id);
        Assert.Equal(50m, refreshedOldBucket.Weight); // old snapshot itself is untouched.
    }

    [Fact]
    public async Task RollClassInstanceForwardAsync_Throws_WhenInstanceIsNotActive()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var year = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A", Status = ClassInstanceStatus.Archived };
        context.AddRange(year, group, instance);
        await context.SaveChangesAsync();

        var service = new RolloverService(db.CreateContextFactory(), new FixedClock(DateTime.Now));

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => service.RollClassInstanceForwardAsync(instance.Id, year.Id, "6A"));
    }

    [Fact]
    public async Task ArchiveClassInstanceAsync_UnassignsTeacher_AndOptionallyEndsClassGroup()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var year = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var teacher = new Teacher { DisplayName = "Frau Meier" };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "9A", Teacher = teacher };
        context.AddRange(year, teacher, group, instance);
        await context.SaveChangesAsync();

        var service = new RolloverService(db.CreateContextFactory(), new FixedClock(DateTime.Now));
        await service.ArchiveClassInstanceAsync(instance.Id, endClassGroup: true);

        await using var verify = db.CreateContext();
        var refreshedInstance = await verify.ClassInstances.FirstAsync(ci => ci.Id == instance.Id);
        var refreshedGroup = await verify.ClassGroups.FirstAsync(g => g.Id == group.Id);

        Assert.Equal(ClassInstanceStatus.Archived, refreshedInstance.Status);
        Assert.Null(refreshedInstance.TeacherId);
        Assert.Equal(ClassGroupStatus.Ended, refreshedGroup.Status);
    }

    private static readonly DateTime Now = new(2026, 7, 15);

    private sealed record Years(int OldId, int TargetId);

    private static RolloverService CreateService(SqliteTestDatabase db) => new(db.CreateContextFactory(), new FixedClock(Now));

    private static async Task<Years> SeedYearsAsync(
        SqliteTestDatabase db,
        SchoolYearStatus oldStatus = SchoolYearStatus.Active,
        SchoolYearStatus targetStatus = SchoolYearStatus.Planned)
    {
        await using var context = db.CreateContext();
        var oldYear = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31), Status = oldStatus };
        var targetYear = new SchoolYear { Label = "2026/27", StartDate = new DateTime(2026, 8, 1), EndDate = new DateTime(2027, 7, 31), Status = targetStatus };
        context.AddRange(oldYear, targetYear);
        await context.SaveChangesAsync();
        return new Years(oldYear.Id, targetYear.Id);
    }

    private static async Task<(int ClassId, int GroupId)> AddClassAsync(
        SqliteTestDatabase db, int yearId, string name, ClassInstanceStatus status = ClassInstanceStatus.Active)
    {
        await using var context = db.CreateContext();
        var group = new ClassGroup { CohortLabel = $"Cohort {name}", FoundedInSchoolYearId = yearId };
        var instance = new ClassInstance { ClassGroup = group, SchoolYearId = yearId, Name = name, Status = status };
        context.AddRange(group, instance);
        await context.SaveChangesAsync();
        return (instance.Id, group.Id);
    }

    private static async Task<int> AddStudentAsync(
        SqliteTestDatabase db, int classId, string lastName, DateTime joinedOn, DateTime? leftOn = null)
    {
        await using var context = db.CreateContext();
        var student = new Student { FirstName = "Student", LastName = lastName };
        context.Students.Add(student);
        context.Enrollments.Add(new Enrollment { Student = student, ClassInstanceId = classId, JoinedOn = joinedOn, LeftOn = leftOn });
        await context.SaveChangesAsync();
        return student.Id;
    }

    private static async Task AssertNothingCreatedInTargetAsync(SqliteTestDatabase db, int targetYearId)
    {
        await using var verify = db.CreateContext();
        Assert.Empty(await verify.ClassInstances.Where(ci => ci.SchoolYearId == targetYearId).ToListAsync());
    }

    [Fact]
    public async Task RollClassInstanceForwardAsync_Throws_WhenTargetYearIsMissing()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedYearsAsync(db);
        var (classId, _) = await AddClassAsync(db, years.OldId, "5A");

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RollClassInstanceForwardAsync(classId, 4711, "6A"));

        await using var verify = db.CreateContext();
        Assert.Equal(ClassInstanceStatus.Active, (await verify.ClassInstances.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(SchoolYearStatus.Active)]
    [InlineData(SchoolYearStatus.Closed)]
    public async Task RollClassInstanceForwardAsync_Throws_WhenTargetYearIsNotPlanned(SchoolYearStatus targetStatus)
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedYearsAsync(db, targetStatus: targetStatus);
        var (classId, _) = await AddClassAsync(db, years.OldId, "5A");

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RollClassInstanceForwardAsync(classId, years.TargetId, "6A"));

        await AssertNothingCreatedInTargetAsync(db, years.TargetId);
        await using var verify = db.CreateContext();
        Assert.Equal(ClassInstanceStatus.Active, (await verify.ClassInstances.SingleAsync()).Status);
    }

    [Fact]
    public async Task RollClassInstanceForwardAsync_Throws_WhenTargetIsTheSourceYear()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedYearsAsync(db, oldStatus: SchoolYearStatus.Planned);
        var (classId, _) = await AddClassAsync(db, years.OldId, "5A");

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RollClassInstanceForwardAsync(classId, years.OldId, "6A"));

        await using var verify = db.CreateContext();
        var instance = await verify.ClassInstances.SingleAsync();
        Assert.Equal(ClassInstanceStatus.Active, instance.Status);
    }

    [Fact]
    public async Task RollClassInstanceForwardAsync_Throws_WhenTargetStartsBeforeSourceYear()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedYearsAsync(db);
        var (classId, _) = await AddClassAsync(db, years.TargetId, "5A");
        await using (var context = db.CreateContext())
        {
            // The class lives in the later year; the "target" is the earlier, Planned one.
            (await context.SchoolYears.SingleAsync(sy => sy.Id == years.OldId)).Status = SchoolYearStatus.Planned;
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RollClassInstanceForwardAsync(classId, years.OldId, "4A"));

        await using var verify = db.CreateContext();
        Assert.Equal(ClassInstanceStatus.Active, (await verify.ClassInstances.SingleAsync()).Status);
        Assert.Equal(1, await verify.ClassInstances.CountAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RollClassInstanceForwardAsync_Throws_ForEmptyName(string name)
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedYearsAsync(db);
        var (classId, _) = await AddClassAsync(db, years.OldId, "5A");

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RollClassInstanceForwardAsync(classId, years.TargetId, name));

        await AssertNothingCreatedInTargetAsync(db, years.TargetId);
        await using var verify = db.CreateContext();
        Assert.Equal(ClassInstanceStatus.Active, (await verify.ClassInstances.SingleAsync()).Status);
    }

    [Fact]
    public async Task RollClassInstanceForwardAsync_TrimsName()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedYearsAsync(db);
        var (classId, _) = await AddClassAsync(db, years.OldId, "5A");

        var created = await CreateService(db).RollClassInstanceForwardAsync(classId, years.TargetId, "  6A  ");

        Assert.Equal("6A", created.Name);
        await using var verify = db.CreateContext();
        Assert.Equal("6A", (await verify.ClassInstances.SingleAsync(ci => ci.Id == created.Id)).Name);
    }

    [Fact]
    public async Task RollClassInstanceForwardAsync_Throws_WhenClassGroupAlreadyHasInstanceInTargetYear()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedYearsAsync(db);
        var (classId, groupId) = await AddClassAsync(db, years.OldId, "5A");
        await using (var context = db.CreateContext())
        {
            context.ClassInstances.Add(new ClassInstance { ClassGroupId = groupId, SchoolYearId = years.TargetId, Name = "6A" });
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RollClassInstanceForwardAsync(classId, years.TargetId, "6B"));

        await using var verify = db.CreateContext();
        Assert.Equal(ClassInstanceStatus.Active, (await verify.ClassInstances.SingleAsync(ci => ci.Id == classId)).Status);
        Assert.Equal(1, await verify.ClassInstances.CountAsync(ci => ci.SchoolYearId == years.TargetId));
    }

    [Fact]
    public async Task RollClassInstanceForwardAsync_RollsBackEverything_WhenStudentIsAlreadyEnrolledInTargetYear()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedYearsAsync(db);
        var (classId, _) = await AddClassAsync(db, years.OldId, "5A");
        var studentId = await AddStudentAsync(db, classId, "A", new DateTime(2025, 8, 1));
        var (otherClassId, _) = await AddClassAsync(db, years.TargetId, "6X");
        await using (var context = db.CreateContext())
        {
            context.Enrollments.Add(new Enrollment { StudentId = studentId, ClassInstanceId = otherClassId, JoinedOn = new DateTime(2026, 8, 1) });
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RollClassInstanceForwardAsync(classId, years.TargetId, "6A"));

        await using var verify = db.CreateContext();
        Assert.Equal(ClassInstanceStatus.Active, (await verify.ClassInstances.SingleAsync(ci => ci.Id == classId)).Status);
        Assert.Equal(1, await verify.ClassInstances.CountAsync(ci => ci.SchoolYearId == years.TargetId));
        Assert.Equal(2, await verify.Enrollments.CountAsync());
    }

    [Fact]
    public async Task GetClassRolloverStatesAsync_CountsOnlyCurrentlyActiveStudents_ForActiveClass()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedYearsAsync(db);
        var (classId, _) = await AddClassAsync(db, years.OldId, "5A");
        await AddStudentAsync(db, classId, "A", new DateTime(2025, 8, 1));
        await AddStudentAsync(db, classId, "B", new DateTime(2025, 8, 1), leftOn: new DateTime(2026, 1, 31));
        await AddStudentAsync(db, classId, "C", new DateTime(2026, 9, 1));

        var states = await CreateService(db).GetClassRolloverStatesAsync(years.OldId, years.TargetId);

        var state = Assert.Single(states);
        Assert.Equal(classId, state.ClassInstanceId);
        Assert.Equal("5A", state.Name);
        Assert.Equal(ClassInstanceStatus.Active, state.Status);
        Assert.Equal(1, state.CarryOverStudentCount);
        Assert.Equal("6A", state.SuggestedName);
        Assert.Null(state.SuccessorClassInstanceId);
        Assert.Null(state.SuccessorName);
        Assert.False(state.IsClassGroupEnded);
    }

    [Fact]
    public async Task GetClassRolloverStatesAsync_ReportsSuccessor_ForRolledForwardClass()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedYearsAsync(db);
        var (classId, _) = await AddClassAsync(db, years.OldId, "5A");
        await AddStudentAsync(db, classId, "A", new DateTime(2025, 8, 1));
        await AddStudentAsync(db, classId, "B", new DateTime(2025, 8, 1));
        var service = CreateService(db);
        var successor = await service.RollClassInstanceForwardAsync(classId, years.TargetId, "6A");

        var states = await service.GetClassRolloverStatesAsync(years.OldId, years.TargetId);

        var state = Assert.Single(states);
        Assert.Equal(ClassInstanceStatus.RolledForward, state.Status);
        Assert.Equal(2, state.CarryOverStudentCount);
        Assert.Equal(successor.Id, state.SuccessorClassInstanceId);
        Assert.Equal("6A", state.SuccessorName);
    }

    [Fact]
    public async Task GetClassRolloverStatesAsync_ReportsEndedGroup_ForArchivedClass()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedYearsAsync(db);
        var (classId, _) = await AddClassAsync(db, years.OldId, "9A");
        await AddStudentAsync(db, classId, "A", new DateTime(2025, 8, 1));
        var service = CreateService(db);
        await service.ArchiveClassInstanceAsync(classId, endClassGroup: true);

        var states = await service.GetClassRolloverStatesAsync(years.OldId, years.TargetId);

        var state = Assert.Single(states);
        Assert.Equal(ClassInstanceStatus.Archived, state.Status);
        Assert.Equal(0, state.CarryOverStudentCount);
        Assert.True(state.IsClassGroupEnded);
        Assert.Null(state.SuccessorClassInstanceId);
    }

    [Fact]
    public async Task GetClassRolloverStatesAsync_Throws_ForMissingYear()
    {
        using var db = new SqliteTestDatabase();

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).GetClassRolloverStatesAsync(4711, null));
    }

    [Fact]
    public async Task GetClassRolloverStatesAsync_Throws_ForMissingTargetYear()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedYearsAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).GetClassRolloverStatesAsync(years.OldId, 4711));
    }

    [Fact]
    public async Task EndOfYearFlow_CanResumeAndComplete_AfterOneClassFailedOnConflict()
    {
        using var db = new SqliteTestDatabase();
        var clock = new FixedClock(Now);
        var years = await SeedYearsAsync(db);
        var (classAId, _) = await AddClassAsync(db, years.OldId, "5A");
        var (classBId, _) = await AddClassAsync(db, years.OldId, "5B");
        await AddStudentAsync(db, classAId, "A", new DateTime(2025, 8, 1));
        var conflictedStudentId = await AddStudentAsync(db, classBId, "B", new DateTime(2025, 8, 1));
        var (blockerClassId, _) = await AddClassAsync(db, years.TargetId, "6X");
        int blockerEnrollmentId;
        await using (var context = db.CreateContext())
        {
            var blocker = new Enrollment { StudentId = conflictedStudentId, ClassInstanceId = blockerClassId, JoinedOn = new DateTime(2026, 8, 1) };
            context.Enrollments.Add(blocker);
            await context.SaveChangesAsync();
            blockerEnrollmentId = blocker.Id;
        }

        var rollover = new RolloverService(db.CreateContextFactory(), clock);
        var close = new YearCloseService(db.CreateContextFactory(), clock, NullLogger<YearCloseService>.Instance);

        await rollover.RollClassInstanceForwardAsync(classAId, years.TargetId, "6A");
        await Assert.ThrowsAsync<InvariantViolationException>(() => rollover.RollClassInstanceForwardAsync(classBId, years.TargetId, "6B"));

        var midStates = await rollover.GetClassRolloverStatesAsync(years.OldId, years.TargetId);
        Assert.Equal(ClassInstanceStatus.RolledForward, midStates.Single(s => s.ClassInstanceId == classAId).Status);
        Assert.Equal(ClassInstanceStatus.Active, midStates.Single(s => s.ClassInstanceId == classBId).Status);
        await Assert.ThrowsAsync<InvariantViolationException>(() => close.CloseAndActivateNextYearAsync(years.OldId, years.TargetId));

        await using (var context = db.CreateContext())
        {
            // Resolve the conflict in test setup (the product never deletes) and retry only the failed class.
            context.Enrollments.Remove(await context.Enrollments.SingleAsync(e => e.Id == blockerEnrollmentId));
            await context.SaveChangesAsync();
        }

        await rollover.RollClassInstanceForwardAsync(classBId, years.TargetId, "6B");
        await close.CloseAndActivateNextYearAsync(years.OldId, years.TargetId);

        await using var verify = db.CreateContext();
        Assert.Equal(SchoolYearStatus.Closed, (await verify.SchoolYears.SingleAsync(sy => sy.Id == years.OldId)).Status);
        Assert.Equal(SchoolYearStatus.Active, (await verify.SchoolYears.SingleAsync(sy => sy.Id == years.TargetId)).Status);
    }
}
