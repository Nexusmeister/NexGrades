using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Auditing;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class ClassServiceTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0);

    private sealed class Env : IDisposable
    {
        public SqliteTestDatabase Db { get; }
        public ClassService Service { get; }

        public Env(bool failAfterClassSave = false)
        {
            var clock = new FixedClock(Now);
            var audit = new ClosedYearAuditInterceptor(clock, new UnlockReasonStore());
            Db = failAfterClassSave
                ? new SqliteTestDatabase(audit, new ThrowAfterSaveInterceptor<ClassInstance>())
                : new SqliteTestDatabase(audit);
            Service = new ClassService(Db.CreateContextFactory());
        }

        /// <summary>A context without any interceptor, so seeding is allowed in any year state.</summary>
        public AppDbContext SeedContext() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Db.DbPath}").Options);

        public void Dispose() => Db.Dispose();
    }

    private static async Task<int> SeedYearAsync(Env env, SchoolYearStatus status, int startYear = 2025)
    {
        await using var context = env.SeedContext();
        var year = new SchoolYear
        {
            Label = $"{startYear}/{startYear + 1}",
            StartDate = new DateTime(startYear, 8, 1),
            EndDate = new DateTime(startYear + 1, 7, 31),
            Status = status,
            ClosedOn = status == SchoolYearStatus.Closed ? new DateTime(startYear + 1, 7, 31) : null,
        };
        context.SchoolYears.Add(year);
        await context.SaveChangesAsync();
        return year.Id;
    }

    private static async Task<int> SeedClassAsync(
        Env env,
        SchoolYearStatus status = SchoolYearStatus.Active,
        ClassInstanceStatus classStatus = ClassInstanceStatus.Active,
        DateTime? unlockedUntil = null)
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
        var instance = new ClassInstance
        {
            ClassGroup = group,
            SchoolYear = year,
            Name = "Test 5A",
            Status = classStatus,
        };
        context.AddRange(year, group, instance);
        await context.SaveChangesAsync();
        return instance.Id;
    }

    private static async Task<ClassInstance> LoadInstanceAsync(Env env, int id)
    {
        await using var context = env.Db.CreateContext();
        return await context.ClassInstances.AsNoTracking().SingleAsync(ci => ci.Id == id);
    }

    private static async Task<List<AuditEntry>> ClassAuditEntriesAsync(Env env)
    {
        await using var context = env.Db.CreateContext();
        return await context.AuditEntries.AsNoTracking()
            .Where(a => a.EntityType == nameof(ClassInstance)).ToListAsync();
    }

    private static async Task AssertNoClassRowsAsync(Env env)
    {
        await using var context = env.Db.CreateContext();
        Assert.Equal(0, await context.ClassGroups.CountAsync());
        Assert.Equal(0, await context.ClassInstances.CountAsync());
    }

    // ---------- CreateClassAsync ----------

    [Fact]
    public async Task CreateClassAsync_CreatesGroupAndInstance_InActiveYear()
    {
        using var env = new Env();
        await SeedYearAsync(env, SchoolYearStatus.Closed, 2023);
        await SeedYearAsync(env, SchoolYearStatus.Planned, 2026);
        var activeYearId = await SeedYearAsync(env, SchoolYearStatus.Active, 2025);

        var created = await env.Service.CreateClassAsync("Test 7B");

        await using var verify = env.Db.CreateContext();
        var instance = await verify.ClassInstances.AsNoTracking().SingleAsync();
        Assert.Equal(created.Id, instance.Id);
        Assert.Equal("Test 7B", instance.Name);
        Assert.Equal(activeYearId, instance.SchoolYearId);
        Assert.Equal(ClassInstanceStatus.Active, instance.Status);
        Assert.Null(instance.TeacherId);
        var group = await verify.ClassGroups.AsNoTracking().SingleAsync();
        Assert.Equal(group.Id, instance.ClassGroupId);
        Assert.Equal("Test 7B", group.CohortLabel);
        Assert.Equal(activeYearId, group.FoundedInSchoolYearId);
    }

    [Fact]
    public async Task CreateClassAsync_TrimsName()
    {
        using var env = new Env();
        await SeedYearAsync(env, SchoolYearStatus.Active);

        await env.Service.CreateClassAsync("  Test 7B \t");

        await using var verify = env.Db.CreateContext();
        Assert.Equal("Test 7B", (await verify.ClassInstances.AsNoTracking().SingleAsync()).Name);
        Assert.Equal("Test 7B", (await verify.ClassGroups.AsNoTracking().SingleAsync()).CohortLabel);
    }

    [Fact]
    public async Task CreateClassAsync_Throws_WhenNoActiveYear()
    {
        using var env = new Env();
        await SeedYearAsync(env, SchoolYearStatus.Planned, 2026);
        await SeedYearAsync(env, SchoolYearStatus.Closed, 2024);

        await Assert.ThrowsAsync<InvariantViolationException>(() => env.Service.CreateClassAsync("Test 7B"));

        await AssertNoClassRowsAsync(env);
    }

    [Fact]
    public async Task CreateClassAsync_Throws_WhenMoreThanOneActiveYear()
    {
        using var env = new Env();
        await SeedYearAsync(env, SchoolYearStatus.Active, 2024);
        await SeedYearAsync(env, SchoolYearStatus.Active, 2025);

        await Assert.ThrowsAsync<InvariantViolationException>(() => env.Service.CreateClassAsync("Test 7B"));

        await AssertNoClassRowsAsync(env);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task CreateClassAsync_Throws_ForEmptyOrWhitespaceName(string name)
    {
        using var env = new Env();
        await SeedYearAsync(env, SchoolYearStatus.Active);

        await Assert.ThrowsAsync<InvariantViolationException>(() => env.Service.CreateClassAsync(name));

        await AssertNoClassRowsAsync(env);
    }

    [Fact]
    public async Task CreateClassAsync_PersistsNothing_WhenSaveFailsBeforeCommit()
    {
        using var env = new Env(failAfterClassSave: true);
        await SeedYearAsync(env, SchoolYearStatus.Active);

        await Assert.ThrowsAsync<InvalidOperationException>(() => env.Service.CreateClassAsync("Test 7B"));

        await AssertNoClassRowsAsync(env);
    }

    // ---------- RenameClassAsync ----------

    [Fact]
    public async Task RenameClassAsync_UpdatesInstanceName_AndLeavesCohortLabel()
    {
        using var env = new Env();
        var id = await SeedClassAsync(env);

        await env.Service.RenameClassAsync(id, "  Test 5C ");

        var instance = await LoadInstanceAsync(env, id);
        Assert.Equal("Test 5C", instance.Name);
        await using var verify = env.Db.CreateContext();
        Assert.Equal("Cohort Test", (await verify.ClassGroups.AsNoTracking().SingleAsync()).CohortLabel);
    }

    [Fact]
    public async Task RenameClassAsync_Throws_ForUnknownClass()
    {
        using var env = new Env();
        await SeedClassAsync(env);

        await Assert.ThrowsAsync<InvariantViolationException>(() => env.Service.RenameClassAsync(9999, "Test 5C"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task RenameClassAsync_Throws_ForEmptyName(string name)
    {
        using var env = new Env();
        var id = await SeedClassAsync(env);

        await Assert.ThrowsAsync<InvariantViolationException>(() => env.Service.RenameClassAsync(id, name));

        Assert.Equal("Test 5A", (await LoadInstanceAsync(env, id)).Name);
    }

    [Theory]
    [InlineData(ClassInstanceStatus.RolledForward)]
    [InlineData(ClassInstanceStatus.Archived)]
    public async Task RenameClassAsync_Throws_ForNonActiveInstance(ClassInstanceStatus status)
    {
        using var env = new Env();
        var id = await SeedClassAsync(env, classStatus: status);

        await Assert.ThrowsAsync<InvariantViolationException>(() => env.Service.RenameClassAsync(id, "Test 5C"));

        Assert.Equal("Test 5A", (await LoadInstanceAsync(env, id)).Name);
    }

    [Fact]
    public async Task RenameClassAsync_Throws_ForLockedClosedYear()
    {
        using var env = new Env();
        var id = await SeedClassAsync(env, SchoolYearStatus.Closed);

        await Assert.ThrowsAsync<ClosedYearException>(() => env.Service.RenameClassAsync(id, "Test 5C"));

        Assert.Equal("Test 5A", (await LoadInstanceAsync(env, id)).Name);
        Assert.Empty(await ClassAuditEntriesAsync(env));
    }

    [Fact]
    public async Task RenameClassAsync_WritesAuditEntry_ForUnlockedClosedYear()
    {
        using var env = new Env();
        var id = await SeedClassAsync(env, SchoolYearStatus.Closed, unlockedUntil: Now.AddDays(1));

        await env.Service.RenameClassAsync(id, "Test 5C");

        Assert.Equal("Test 5C", (await LoadInstanceAsync(env, id)).Name);
        var entry = Assert.Single(await ClassAuditEntriesAsync(env));
        Assert.Equal(id, entry.EntityId);
        Assert.Equal("Name", entry.Field);
        Assert.Equal("Test 5A", entry.OldValue);
        Assert.Equal("Test 5C", entry.NewValue);
    }

    [Fact]
    public async Task RenameClassAsync_WritesNothing_WhenNameUnchanged()
    {
        using var env = new Env();
        var id = await SeedClassAsync(env, SchoolYearStatus.Closed, unlockedUntil: Now.AddDays(1));

        await env.Service.RenameClassAsync(id, " Test 5A ");

        Assert.Equal("Test 5A", (await LoadInstanceAsync(env, id)).Name);
        Assert.Empty(await ClassAuditEntriesAsync(env));
    }
}
