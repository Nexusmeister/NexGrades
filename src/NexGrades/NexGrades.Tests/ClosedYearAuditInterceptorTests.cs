using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Auditing;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class ClosedYearAuditInterceptorTests
{
    private static async Task<(SqliteTestDatabase Db, int YearId, int ClassInstanceId)> SeedClosedYearAsync(
        FixedClock clock, DateTime? unlockedUntil = null, string? unlockReason = null)
    {
        var interceptor = new ClosedYearAuditInterceptor(clock);
        var db = new SqliteTestDatabase(interceptor);
        await using var seedContext = db.CreateContext();
        var year = new SchoolYear
        {
            Label = "2024/25",
            StartDate = new DateTime(2024, 8, 1),
            EndDate = new DateTime(2025, 7, 31),
            Status = SchoolYearStatus.Closed,
            ClosedOn = new DateTime(2025, 7, 31),
            UnlockedUntil = unlockedUntil,
            UnlockReason = unlockReason,
        };
        var group = new ClassGroup { CohortLabel = "Kohorte 2018", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "6A" };
        seedContext.AddRange(year, group, instance);
        await seedContext.SaveChangesAsync();

        return (db, year.Id, instance.Id);
    }

    [Fact]
    public async Task Write_ToLockedClosedYear_Throws_AndPersistsNothing()
    {
        var clock = new FixedClock(new DateTime(2025, 9, 1));
        var (db, _, instanceId) = await SeedClosedYearAsync(clock, unlockedUntil: null);
        using var _ = db;

        await using var context = db.CreateContext();
        var student = new Student { FirstName = "Anna", LastName = "Schmidt" };
        context.Students.Add(student);
        context.Enrollments.Add(new Enrollment { Student = student, ClassInstanceId = instanceId, JoinedOn = clock.Now });

        await Assert.ThrowsAsync<ClosedYearException>(() => context.SaveChangesAsync());

        await using var verifyContext = db.CreateContext();
        Assert.Empty(await verifyContext.Enrollments.ToListAsync());
    }

    [Fact]
    public async Task Write_ToUnlockedClosedYear_Succeeds_AndProducesAnAuditEntry()
    {
        var clock = new FixedClock(new DateTime(2025, 9, 1));
        var (db, _, instanceId) = await SeedClosedYearAsync(clock, unlockedUntil: new DateTime(2025, 9, 2), unlockReason: "Corrected a transcription error");
        using var _ = db;

        await using var context = db.CreateContext();
        var student = new Student { FirstName = "Anna", LastName = "Schmidt" };
        context.Students.Add(student);
        context.Enrollments.Add(new Enrollment { Student = student, ClassInstanceId = instanceId, JoinedOn = clock.Now });
        await context.SaveChangesAsync();

        await using var verifyContext = db.CreateContext();
        var enrollments = await verifyContext.Enrollments.ToListAsync();
        Assert.Single(enrollments);

        var auditEntries = await verifyContext.AuditEntries.Where(a => a.EntityType == nameof(Enrollment)).ToListAsync();
        Assert.NotEmpty(auditEntries);
        Assert.All(auditEntries, a => Assert.Equal("Corrected a transcription error", a.Reason));
    }

    private static UnlockService CreateUnlockService(SqliteTestDatabase db, FixedClock clock) =>
        new(db.CreateContextFactory(), clock, NullLogger<UnlockService>.Instance);

    [Fact]
    public async Task Write_AfterUnlockAsync_CarriesTheReason_WithFreshContextAndInterceptor()
    {
        var clock = new FixedClock(new DateTime(2025, 9, 1));
        var (db, yearId, instanceId) = await SeedClosedYearAsync(clock);
        using var _ = db;
        await CreateUnlockService(db, clock).UnlockAsync(yearId, clock.Now.AddHours(1), "  Corrected a typo ");

        // A restart: only the database file survives, so the reason must come from the database.
        var restartedOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(db.ConnectionString)
            .AddInterceptors(new ClosedYearAuditInterceptor(clock))
            .Options;
        await using (var context = new AppDbContext(restartedOptions))
        {
            var student = new Student { FirstName = "Anna", LastName = "Schmidt" };
            context.Students.Add(student);
            context.Enrollments.Add(new Enrollment { Student = student, ClassInstanceId = instanceId, JoinedOn = clock.Now });
            await context.SaveChangesAsync();
        }

        await using var verifyContext = db.CreateContext();
        var auditEntries = await verifyContext.AuditEntries.Where(a => a.EntityType == nameof(Enrollment)).ToListAsync();
        Assert.NotEmpty(auditEntries);
        Assert.All(auditEntries, a => Assert.Equal("Corrected a typo", a.Reason));
    }

    [Fact]
    public async Task Write_AfterRelockAsync_Throws_ClosedYearException_AndPersistsNothing()
    {
        var clock = new FixedClock(new DateTime(2025, 9, 1));
        var (db, yearId, instanceId) = await SeedClosedYearAsync(clock);
        using var _ = db;
        var service = CreateUnlockService(db, clock);
        await service.UnlockAsync(yearId, clock.Now.AddHours(1), "Corrected a typo");
        await service.RelockAsync(yearId);

        await using var context = db.CreateContext();
        var student = new Student { FirstName = "Anna", LastName = "Schmidt" };
        context.Students.Add(student);
        context.Enrollments.Add(new Enrollment { Student = student, ClassInstanceId = instanceId, JoinedOn = clock.Now });

        await Assert.ThrowsAsync<ClosedYearException>(() => context.SaveChangesAsync());

        await using var verifyContext = db.CreateContext();
        Assert.Empty(await verifyContext.Enrollments.ToListAsync());
        Assert.Empty(await verifyContext.AuditEntries.ToListAsync());
    }

    [Fact]
    public async Task Write_ToUnlockedClosedYear_Throws_OnceUnlockWindowHasExpired()
    {
        var clock = new FixedClock(new DateTime(2025, 9, 5));
        // Unlock window already in the past relative to the clock.
        var (db, _, instanceId) = await SeedClosedYearAsync(clock, unlockedUntil: new DateTime(2025, 9, 2), unlockReason: "Should not matter");
        using var _ = db;

        await using var context = db.CreateContext();
        var student = new Student { FirstName = "Anna", LastName = "Schmidt" };
        context.Students.Add(student);
        context.Enrollments.Add(new Enrollment { Student = student, ClassInstanceId = instanceId, JoinedOn = clock.Now });

        await Assert.ThrowsAsync<ClosedYearException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Delete_IsAlwaysRejected_EvenForAnEntityNotReachableFromAnySchoolYear()
    {
        var clock = new FixedClock(DateTime.Now);
        var interceptor = new ClosedYearAuditInterceptor(clock);
        using var db = new SqliteTestDatabase(interceptor);

        await using var seedContext = db.CreateContext();
        var subject = new Subject { Name = "Mathematik", ShortCode = "MA" };
        seedContext.Subjects.Add(subject);
        await seedContext.SaveChangesAsync();

        await using var context = db.CreateContext();
        var tracked = await context.Subjects.FirstAsync(s => s.Id == subject.Id);
        context.Subjects.Remove(tracked);

        await Assert.ThrowsAsync<InvariantViolationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Write_ToAnOpenSchoolYear_IsUnrestricted_AndProducesNoAuditEntry()
    {
        var clock = new FixedClock(DateTime.Now);
        var interceptor = new ClosedYearAuditInterceptor(clock);
        using var db = new SqliteTestDatabase(interceptor);

        await using var seedContext = db.CreateContext();
        var year = new SchoolYear
        {
            Label = "2025/26",
            StartDate = new DateTime(2025, 8, 1),
            EndDate = new DateTime(2026, 7, 31),
            Status = SchoolYearStatus.Active,
        };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A" };
        seedContext.AddRange(year, group, instance);
        await seedContext.SaveChangesAsync();

        await using var context = db.CreateContext();
        var student = new Student { FirstName = "Anna", LastName = "Schmidt" };
        context.Students.Add(student);
        context.Enrollments.Add(new Enrollment { Student = student, ClassInstance = await context.ClassInstances.FirstAsync(), JoinedOn = clock.Now });
        await context.SaveChangesAsync();

        await using var verifyContext = db.CreateContext();
        Assert.Empty(await verifyContext.AuditEntries.ToListAsync());
    }
}
