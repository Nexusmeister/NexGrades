using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexGrades.Data.Entities;
using NexGrades.Domain.Auditing;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class FinalGradeOverrideTests
{
    private const string Reason = "Corrected a final grade";

    private static readonly FixedClock Clock = new(new DateTime(2026, 8, 15, 10, 0, 0));

    private static readonly DateTime UnlockedUntil = new(2026, 8, 16);

    private sealed record Seeded(SqliteTestDatabase Db, GradeSheetSeed Seed, int StudentId);

    private static async Task<Seeded> SeedAsync(
        bool withInterceptor = false, DateTime? leftOn = null, bool exempt = false)
    {
        var db = withInterceptor
            ? new SqliteTestDatabase(new ClosedYearAuditInterceptor(Clock))
            : new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha", leftOn: leftOn);
        if (exempt)
        {
            await seed.AddExemptionAsync(student);
        }

        return new Seeded(db, seed, student);
    }

    private static GradeService CreateService(Seeded s) => new(s.Db.CreateContextFactory(), Clock);

    private static async Task<List<FinalGrade>> LoadAsync(Seeded s)
    {
        await using var verify = s.Db.CreateContext();
        return await verify.FinalGrades.ToListAsync();
    }

    // Set: allowed

    [Fact]
    public async Task SetFinalGradeOverrideAsync_StoresOverride_ForEnrolledStudent()
    {
        var s = await SeedAsync();
        using var _ = s.Db;

        await CreateService(s).SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 3m);

        var row = Assert.Single(await LoadAsync(s));
        Assert.Equal(s.Seed.SubjectInstanceId, row.SubjectInstanceId);
        Assert.Equal(s.StudentId, row.StudentId);
        Assert.Equal(3m, row.OverrideValue);
    }

    [Fact]
    public async Task SetFinalGradeOverrideAsync_UpdatesSameRow_WhenSetAgain()
    {
        var s = await SeedAsync();
        using var _ = s.Db;
        var service = CreateService(s);

        await service.SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 3m);
        await service.SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 4m);

        var row = Assert.Single(await LoadAsync(s));
        Assert.Equal(4m, row.OverrideValue);
    }

    [Fact]
    public async Task SetFinalGradeOverrideAsync_ReusesRow_AfterClear()
    {
        var s = await SeedAsync();
        using var _ = s.Db;
        var service = CreateService(s);
        await service.SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 3m);
        var firstId = Assert.Single(await LoadAsync(s)).Id;
        await service.ClearFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId);

        await service.SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 2m);

        var row = Assert.Single(await LoadAsync(s));
        Assert.Equal(firstId, row.Id);
        Assert.Equal(2m, row.OverrideValue);
    }

    [Fact]
    public async Task SetFinalGradeOverrideAsync_Succeeds_ForStudentWithoutGrades()
    {
        var s = await SeedAsync();
        using var _ = s.Db;

        await CreateService(s).SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 5m);

        Assert.Equal(5m, Assert.Single(await LoadAsync(s)).OverrideValue);
    }

    [Fact]
    public async Task SetFinalGradeOverrideAsync_Succeeds_ForStudentWhoLeft()
    {
        var s = await SeedAsync(leftOn: new DateTime(2025, 12, 1));
        using var _ = s.Db;

        await CreateService(s).SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 2m);

        Assert.Equal(2m, Assert.Single(await LoadAsync(s)).OverrideValue);
    }

    // Set: rejected

    [Theory]
    [InlineData("2.3")]
    [InlineData("0.7")]
    [InlineData("7")]
    public async Task SetFinalGradeOverrideAsync_Throws_ForIllegalFinalGrade(string value)
    {
        var s = await SeedAsync();
        using var _ = s.Db;
        var parsed = decimal.Parse(value, CultureInfo.InvariantCulture);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(s).SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, parsed));

        Assert.Empty(await LoadAsync(s));
    }

    [Fact]
    public async Task SetFinalGradeOverrideAsync_Throws_ForUnknownSubjectInstance()
    {
        var s = await SeedAsync();
        using var _ = s.Db;

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(s).SetFinalGradeOverrideAsync(9999, s.StudentId, 3m));

        Assert.Empty(await LoadAsync(s));
    }

    [Fact]
    public async Task SetFinalGradeOverrideAsync_Throws_ForStudentNotEnrolledInTheClass()
    {
        var s = await SeedAsync();
        using var _ = s.Db;
        int strangerId;
        await using (var context = s.Db.CreateContext())
        {
            var stranger = new Student { FirstName = "Test", LastName = "Stranger" };
            context.Add(stranger);
            await context.SaveChangesAsync();
            strangerId = stranger.Id;
        }

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(s).SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, strangerId, 3m));

        Assert.Empty(await LoadAsync(s));
    }

    [Fact]
    public async Task SetFinalGradeOverrideAsync_Throws_ForExemptStudent()
    {
        var s = await SeedAsync(exempt: true);
        using var _ = s.Db;

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(s).SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 3m));

        Assert.Empty(await LoadAsync(s));
    }

    // Clear

    [Fact]
    public async Task ClearFinalGradeOverrideAsync_SetsOverrideToNull_AndKeepsRow()
    {
        var s = await SeedAsync();
        using var _ = s.Db;
        var service = CreateService(s);
        await service.SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 3m);

        await service.ClearFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId);

        var row = Assert.Single(await LoadAsync(s));
        Assert.Null(row.OverrideValue);
    }

    [Fact]
    public async Task ClearFinalGradeOverrideAsync_Throws_WhenNoRowExists()
    {
        var s = await SeedAsync();
        using var _ = s.Db;

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(s).ClearFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId));

        Assert.Empty(await LoadAsync(s));
    }

    [Fact]
    public async Task ClearFinalGradeOverrideAsync_Throws_WhenAlreadyCleared()
    {
        var s = await SeedAsync();
        using var _ = s.Db;
        var service = CreateService(s);
        await service.SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 3m);
        await service.ClearFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => service.ClearFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId));

        Assert.Null(Assert.Single(await LoadAsync(s)).OverrideValue);
    }

    // Closed years

    [Fact]
    public async Task SetFinalGradeOverrideAsync_Throws_ClosedYearException_InLockedClosedYear()
    {
        var s = await SeedAsync(withInterceptor: true);
        using var _ = s.Db;
        await s.Seed.SetYearStatusAsync(SchoolYearStatus.Closed);

        await Assert.ThrowsAsync<ClosedYearException>(
            () => CreateService(s).SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 3m));

        Assert.Empty(await LoadAsync(s));
    }

    [Fact]
    public async Task ClearFinalGradeOverrideAsync_Throws_ClosedYearException_InLockedClosedYear()
    {
        var s = await SeedAsync(withInterceptor: true);
        using var _ = s.Db;
        await CreateService(s).SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 3m);
        await s.Seed.SetYearStatusAsync(SchoolYearStatus.Closed);

        await Assert.ThrowsAsync<ClosedYearException>(
            () => CreateService(s).ClearFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId));

        Assert.Equal(3m, Assert.Single(await LoadAsync(s)).OverrideValue);
    }

    [Fact]
    public async Task SetFinalGradeOverrideAsync_WritesCreatedAuditEntry_InUnlockedClosedYear()
    {
        var s = await SeedAsync(withInterceptor: true);
        using var _ = s.Db;
        await s.Seed.SetYearStatusAsync(SchoolYearStatus.Closed, UnlockedUntil, Reason);

        await CreateService(s).SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 3m);

        await using var verify = s.Db.CreateContext();
        var entry = Assert.Single(await verify.AuditEntries.Where(a => a.EntityType == nameof(FinalGrade)).ToListAsync());
        Assert.Equal("<created>", entry.Field);
        Assert.Equal(Reason, entry.Reason);
        Assert.Equal(3m, Assert.Single(await LoadAsync(s)).OverrideValue);
    }

    [Fact]
    public async Task ClearFinalGradeOverrideAsync_WritesOverrideValueAuditEntry_InUnlockedClosedYear()
    {
        var s = await SeedAsync(withInterceptor: true);
        using var _ = s.Db;
        await CreateService(s).SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 3m);
        await s.Seed.SetYearStatusAsync(SchoolYearStatus.Closed, UnlockedUntil, Reason);

        await CreateService(s).ClearFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId);

        await using var verify = s.Db.CreateContext();
        var entry = Assert.Single(await verify.AuditEntries.Where(a => a.EntityType == nameof(FinalGrade)).ToListAsync());
        Assert.Equal("OverrideValue", entry.Field);
        Assert.Equal(Reason, entry.Reason);
        Assert.Null(Assert.Single(await LoadAsync(s)).OverrideValue);
    }

    [Fact]
    public async Task SetFinalGradeOverrideAsync_WritesOneAuditEntry_WhenSetTwiceWithTheSameValue()
    {
        var s = await SeedAsync(withInterceptor: true);
        using var _ = s.Db;
        await s.Seed.SetYearStatusAsync(SchoolYearStatus.Closed, UnlockedUntil, Reason);
        var service = CreateService(s);

        await service.SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 3m);
        await service.SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 3m);

        await using var verify = s.Db.CreateContext();
        var entry = Assert.Single(await verify.AuditEntries.Where(a => a.EntityType == nameof(FinalGrade)).ToListAsync());
        Assert.Equal("<created>", entry.Field);
        Assert.Equal(3m, Assert.Single(await LoadAsync(s)).OverrideValue);
    }

    [Fact]
    public async Task SetFinalGradeOverrideAsync_AddsAnotherAuditEntry_WhenTheValueChanges()
    {
        var s = await SeedAsync(withInterceptor: true);
        using var _ = s.Db;
        await s.Seed.SetYearStatusAsync(SchoolYearStatus.Closed, UnlockedUntil, Reason);
        var service = CreateService(s);
        await service.SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 3m);

        await service.SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 4m);

        await using var verify = s.Db.CreateContext();
        var entries = await verify.AuditEntries.Where(a => a.EntityType == nameof(FinalGrade)).ToListAsync();
        Assert.Equal(2, entries.Count);
        var change = Assert.Single(entries, e => e.Field == "OverrideValue");
        Assert.Equal(Reason, change.Reason);
        Assert.Equal(4m, Assert.Single(await LoadAsync(s)).OverrideValue);
    }

    // Data rules

    [Fact]
    public async Task SaveChangesAsync_Throws_WhenRemovingAFinalGrade()
    {
        var s = await SeedAsync(withInterceptor: true);
        using var _ = s.Db;
        await CreateService(s).SetFinalGradeOverrideAsync(s.Seed.SubjectInstanceId, s.StudentId, 3m);

        await using var context = s.Db.CreateContext();
        context.FinalGrades.Remove(await context.FinalGrades.SingleAsync());

        await Assert.ThrowsAsync<InvariantViolationException>(() => context.SaveChangesAsync());

        Assert.Single(await LoadAsync(s));
    }

    [Fact]
    public async Task SaveChangesAsync_Throws_DbUpdateException_ForDuplicateSubjectInstanceAndStudent()
    {
        var s = await SeedAsync();
        using var _ = s.Db;
        await using var context = s.Db.CreateContext();
        context.FinalGrades.Add(new FinalGrade { SubjectInstanceId = s.Seed.SubjectInstanceId, StudentId = s.StudentId, OverrideValue = 2m });
        context.FinalGrades.Add(new FinalGrade { SubjectInstanceId = s.Seed.SubjectInstanceId, StudentId = s.StudentId, OverrideValue = 3m });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Empty(await LoadAsync(s));
    }
}
