using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Auditing;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class StudentServiceTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0);

    private sealed class Env : IDisposable
    {
        public SqliteTestDatabase Db { get; }
        public StudentService Service { get; }

        public Env(bool failAfterEnrollmentSave = false)
        {
            var clock = new FixedClock(Now);
            var audit = new ClosedYearAuditInterceptor(clock, new UnlockReasonStore());
            Db = failAfterEnrollmentSave
                ? new SqliteTestDatabase(audit, new ThrowAfterSaveInterceptor<Enrollment>())
                : new SqliteTestDatabase(audit);
            Service = new StudentService(Db.CreateContextFactory(), clock);
        }

        /// <summary>A context without any interceptor, so seeding is allowed in any year state.</summary>
        public AppDbContext SeedContext() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Db.DbPath}").Options);

        public void Dispose() => Db.Dispose();
    }

    private static async Task<int> SeedClassAsync(
        Env env,
        SchoolYearStatus status = SchoolYearStatus.Active,
        DateTime? unlockedUntil = null,
        ClassInstanceStatus classStatus = ClassInstanceStatus.Active)
    {
        await using var context = env.SeedContext();
        var year = new SchoolYear
        {
            Label = "2025/26",
            StartDate = new DateTime(2025, 8, 1),
            EndDate = new DateTime(2026, 7, 31),
            Status = status,
            UnlockedUntil = unlockedUntil,
            ClosedOn = status == SchoolYearStatus.Closed ? new DateTime(2026, 7, 31) : null,
        };
        var group = new ClassGroup { CohortLabel = "Cohort Test", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "Test 5A", Status = classStatus };
        context.AddRange(year, group, instance);
        await context.SaveChangesAsync();
        return instance.Id;
    }

    private static async Task<int> SeedStudentAsync(Env env, SchoolYearStatus status = SchoolYearStatus.Active)
    {
        var classId = await SeedClassAsync(env, status);
        await using var context = env.SeedContext();
        var student = new Student { FirstName = "Test", LastName = "Person" };
        context.Students.Add(student);
        context.Enrollments.Add(new Enrollment
        {
            Student = student,
            ClassInstanceId = classId,
            JoinedOn = new DateTime(2025, 8, 1),
        });
        await context.SaveChangesAsync();
        return student.Id;
    }

    private static async Task AssertNoStudentRowsAsync(Env env)
    {
        await using var context = env.Db.CreateContext();
        Assert.Equal(0, await context.Students.CountAsync());
        Assert.Equal(0, await context.Enrollments.CountAsync());
    }

    private static async Task AssertNamesAsync(Env env, int studentId, string first, string last)
    {
        await using var context = env.Db.CreateContext();
        var student = await context.Students.AsNoTracking().SingleAsync(s => s.Id == studentId);
        Assert.Equal(first, student.FirstName);
        Assert.Equal(last, student.LastName);
    }

    // ---------- CreateStudentAsync ----------

    [Fact]
    public async Task CreateStudentAsync_CreatesStudentAndEnrollment_InActiveYear()
    {
        using var env = new Env();
        var classId = await SeedClassAsync(env);

        var created = await env.Service.CreateStudentAsync("  Test ", " Person  ", classId);

        await using var verify = env.Db.CreateContext();
        var student = await verify.Students.AsNoTracking().SingleAsync();
        Assert.Equal(created.Id, student.Id);
        Assert.Equal("Test", student.FirstName);
        Assert.Equal("Person", student.LastName);
        var enrollment = await verify.Enrollments.AsNoTracking().SingleAsync();
        Assert.Equal(student.Id, enrollment.StudentId);
        Assert.Equal(classId, enrollment.ClassInstanceId);
        Assert.Equal(Now, enrollment.JoinedOn);
        Assert.Null(enrollment.LeftOn);
    }

    [Fact]
    public async Task CreateStudentAsync_Throws_ForUnknownClass()
    {
        using var env = new Env();
        await SeedClassAsync(env);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => env.Service.CreateStudentAsync("Test", "Person", 9999));

        await AssertNoStudentRowsAsync(env);
    }

    [Theory]
    [InlineData(SchoolYearStatus.Planned, false)]
    [InlineData(SchoolYearStatus.Closed, false)]
    [InlineData(SchoolYearStatus.Closed, true)]
    public async Task CreateStudentAsync_Throws_WhenClassYearIsNotActive(SchoolYearStatus status, bool unlocked)
    {
        using var env = new Env();
        var classId = await SeedClassAsync(env, status, unlocked ? Now.AddDays(1) : null);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => env.Service.CreateStudentAsync("Test", "Person", classId));

        await AssertNoStudentRowsAsync(env);
    }

    [Theory]
    [InlineData(ClassInstanceStatus.RolledForward)]
    [InlineData(ClassInstanceStatus.Archived)]
    public async Task CreateStudentAsync_Throws_ForNonActiveClassInstance(ClassInstanceStatus status)
    {
        using var env = new Env();
        var classId = await SeedClassAsync(env, classStatus: status);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => env.Service.CreateStudentAsync("Test", "Person", classId));

        await AssertNoStudentRowsAsync(env);
    }

    [Theory]
    [InlineData("", "Person")]
    [InlineData("  ", "Person")]
    [InlineData("Test", "")]
    [InlineData("Test", "   ")]
    public async Task CreateStudentAsync_Throws_ForBlankName(string firstName, string lastName)
    {
        using var env = new Env();
        var classId = await SeedClassAsync(env);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => env.Service.CreateStudentAsync(firstName, lastName, classId));

        await AssertNoStudentRowsAsync(env);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateStudentAsync_Throws_ForTooLongName(bool firstNameTooLong)
    {
        using var env = new Env();
        var classId = await SeedClassAsync(env);
        var tooLong = new string('a', StudentService.MaxNameLength + 1);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => env.Service.CreateStudentAsync(
                firstNameTooLong ? tooLong : "Test", firstNameTooLong ? "Person" : tooLong, classId));

        await AssertNoStudentRowsAsync(env);
    }

    [Fact]
    public async Task CreateStudentAsync_Succeeds_AtMaxNameLength()
    {
        using var env = new Env();
        var classId = await SeedClassAsync(env);
        var first = new string('a', StudentService.MaxNameLength);
        var last = new string('b', StudentService.MaxNameLength);

        var created = await env.Service.CreateStudentAsync(first, last, classId);

        await AssertNamesAsync(env, created.Id, first, last);
    }

    [Fact]
    public async Task CreateStudentAsync_PersistsNoStudent_WhenEnrollmentSaveFails()
    {
        using var env = new Env(failAfterEnrollmentSave: true);
        var classId = await SeedClassAsync(env);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => env.Service.CreateStudentAsync("Test", "Person", classId));

        await AssertNoStudentRowsAsync(env);
    }

    [Fact]
    public async Task CreateStudentAsync_CreatesSeparateStudents_ForSameName()
    {
        using var env = new Env();
        var classId = await SeedClassAsync(env);

        var first = await env.Service.CreateStudentAsync("Test", "Person", classId);
        var second = await env.Service.CreateStudentAsync("Test", "Person", classId);

        Assert.NotEqual(first.Id, second.Id);
        await using var verify = env.Db.CreateContext();
        Assert.Equal(2, await verify.Students.CountAsync());
        Assert.Equal(2, await verify.Enrollments.CountAsync());
    }

    // ---------- RenameStudentAsync ----------

    [Fact]
    public async Task RenameStudentAsync_UpdatesTrimmedNames()
    {
        using var env = new Env();
        var id = await SeedStudentAsync(env);

        await env.Service.RenameStudentAsync(id, "  Other ", " Name ");

        await AssertNamesAsync(env, id, "Other", "Name");
    }

    [Fact]
    public async Task RenameStudentAsync_Throws_ForUnknownStudent()
    {
        using var env = new Env();
        await SeedStudentAsync(env);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => env.Service.RenameStudentAsync(9999, "Other", "Name"));
    }

    [Theory]
    [InlineData("", "Name")]
    [InlineData("Other", "  ")]
    public async Task RenameStudentAsync_Throws_ForEmptyName(string firstName, string lastName)
    {
        using var env = new Env();
        var id = await SeedStudentAsync(env);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => env.Service.RenameStudentAsync(id, firstName, lastName));

        await AssertNamesAsync(env, id, "Test", "Person");
    }

    [Fact]
    public async Task RenameStudentAsync_Throws_ForTooLongName()
    {
        using var env = new Env();
        var id = await SeedStudentAsync(env);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => env.Service.RenameStudentAsync(id, new string('a', StudentService.MaxNameLength + 1), "Name"));
        await Assert.ThrowsAsync<InvariantViolationException>(
            () => env.Service.RenameStudentAsync(id, "Other", new string('a', StudentService.MaxNameLength + 1)));

        await AssertNamesAsync(env, id, "Test", "Person");
    }

    [Fact]
    public async Task RenameStudentAsync_Succeeds_ForStudentEnrolledOnlyInLockedClosedYear()
    {
        using var env = new Env();
        var id = await SeedStudentAsync(env, SchoolYearStatus.Closed);

        await env.Service.RenameStudentAsync(id, "Other", "Name");

        await AssertNamesAsync(env, id, "Other", "Name");
    }
}
