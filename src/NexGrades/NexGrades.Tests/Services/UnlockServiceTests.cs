using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;
using NexGrades.Tests.TestSupport;

namespace NexGrades.Tests.Services;

public class UnlockServiceTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0);

    private static async Task<int> SeedClosedYearAsync(SqliteTestDatabase db)
    {
        await using var context = db.CreateContext();
        var year = new SchoolYear
        {
            Label = "2025/26",
            StartDate = new DateTime(2025, 8, 1),
            EndDate = new DateTime(2026, 7, 31),
            Status = SchoolYearStatus.Closed,
            ClosedOn = new DateTime(2026, 7, 20),
        };
        context.Add(year);
        await context.SaveChangesAsync();
        return year.Id;
    }

    private static UnlockService CreateService(SqliteTestDatabase db) =>
        new(db.CreateContextFactory(), new FixedClock(Now), NullLogger<UnlockService>.Instance);

    private static async Task<DateTime?> UnlockedUntilAsync(SqliteTestDatabase db, int yearId)
    {
        await using var verify = db.CreateContext();
        return (await verify.SchoolYears.FindAsync(yearId))!.UnlockedUntil;
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UnlockAsync_Throws_ForBlankReason(string reason)
    {
        using var db = new SqliteTestDatabase();
        var yearId = await SeedClosedYearAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).UnlockAsync(yearId, Now.AddDays(1), reason));

        Assert.Null(await UnlockedUntilAsync(db, yearId));
    }

    [Fact]
    public async Task UnlockAsync_Throws_WhenExpiryIsExactlyNow()
    {
        using var db = new SqliteTestDatabase();
        var yearId = await SeedClosedYearAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).UnlockAsync(yearId, Now, "correction"));

        Assert.Null(await UnlockedUntilAsync(db, yearId));
    }

    [Fact]
    public async Task UnlockAsync_Throws_WhenExpiryIsInThePast()
    {
        using var db = new SqliteTestDatabase();
        var yearId = await SeedClosedYearAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).UnlockAsync(yearId, Now.AddTicks(-1), "correction"));

        Assert.Null(await UnlockedUntilAsync(db, yearId));
    }

    [Fact]
    public async Task UnlockAsync_SetsUnlockedUntil_ForFutureExpiry()
    {
        using var db = new SqliteTestDatabase();
        var yearId = await SeedClosedYearAsync(db);
        var until = Now.AddHours(1);

        await CreateService(db).UnlockAsync(yearId, until, "correction");

        Assert.Equal(until, await UnlockedUntilAsync(db, yearId));
    }

    private static async Task<int> SeedYearAsync(
        SqliteTestDatabase db, SchoolYearStatus status, string label, DateTime start, DateTime? unlockedUntil = null, string? reason = null)
    {
        await using var context = db.CreateContext();
        var year = new SchoolYear
        {
            Label = label,
            StartDate = start,
            EndDate = start.AddYears(1).AddDays(-1),
            Status = status,
            ClosedOn = status == SchoolYearStatus.Closed ? start.AddYears(1) : null,
            UnlockedUntil = unlockedUntil,
            UnlockReason = reason,
        };
        context.Add(year);
        await context.SaveChangesAsync();
        return year.Id;
    }

    private static async Task<SchoolYear> LoadYearAsync(SqliteTestDatabase db, int yearId)
    {
        await using var verify = db.CreateContext();
        return await verify.SchoolYears.AsNoTracking().FirstAsync(sy => sy.Id == yearId);
    }

    [Fact]
    public async Task UnlockAsync_PersistsTrimmedReason()
    {
        using var db = new SqliteTestDatabase();
        var yearId = await SeedClosedYearAsync(db);

        await CreateService(db).UnlockAsync(yearId, Now.AddHours(1), "  fix a typo \t");

        Assert.Equal("fix a typo", (await LoadYearAsync(db, yearId)).UnlockReason);
    }

    [Fact]
    public async Task UnlockAsync_Accepts_ReasonOfExactlyMaxLength()
    {
        using var db = new SqliteTestDatabase();
        var yearId = await SeedClosedYearAsync(db);
        var reason = new string('a', UnlockService.MaxReasonLength);

        await CreateService(db).UnlockAsync(yearId, Now.AddHours(1), reason);

        Assert.Equal(reason, (await LoadYearAsync(db, yearId)).UnlockReason);
    }

    [Fact]
    public async Task UnlockAsync_Throws_ForReasonLongerThanMaxLength()
    {
        using var db = new SqliteTestDatabase();
        var yearId = await SeedClosedYearAsync(db);
        var reason = new string('a', UnlockService.MaxReasonLength + 1);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).UnlockAsync(yearId, Now.AddHours(1), reason));

        var year = await LoadYearAsync(db, yearId);
        Assert.Null(year.UnlockedUntil);
        Assert.Null(year.UnlockReason);
    }

    [Theory]
    [InlineData(SchoolYearStatus.Active)]
    [InlineData(SchoolYearStatus.Planned)]
    public async Task UnlockAsync_Throws_ForYearThatIsNotClosed(SchoolYearStatus status)
    {
        using var db = new SqliteTestDatabase();
        var yearId = await SeedYearAsync(db, status, "2026/27", new DateTime(2026, 8, 1));

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).UnlockAsync(yearId, Now.AddHours(1), "correction"));

        var year = await LoadYearAsync(db, yearId);
        Assert.Null(year.UnlockedUntil);
        Assert.Null(year.UnlockReason);
    }

    [Fact]
    public async Task UnlockAsync_Throws_ForUnknownYear()
    {
        using var db = new SqliteTestDatabase();

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).UnlockAsync(999, Now.AddHours(1), "correction"));
    }

    [Fact]
    public async Task UnlockAsync_ReplacesReasonAndExpiry_WhenAlreadyUnlocked()
    {
        using var db = new SqliteTestDatabase();
        var yearId = await SeedClosedYearAsync(db);
        var service = CreateService(db);
        await service.UnlockAsync(yearId, Now.AddHours(1), "first");

        await service.UnlockAsync(yearId, Now.AddDays(2), "second");

        var year = await LoadYearAsync(db, yearId);
        Assert.Equal(Now.AddDays(2), year.UnlockedUntil);
        Assert.Equal("second", year.UnlockReason);
    }

    [Fact]
    public async Task UnlockAsync_WritesNoAuditEntry()
    {
        using var db = new SqliteTestDatabase(new NexGrades.Domain.Auditing.ClosedYearAuditInterceptor(new FixedClock(Now)));
        var yearId = await SeedClosedYearAsync(db);

        await CreateService(db).UnlockAsync(yearId, Now.AddHours(1), "correction");

        await using var verify = db.CreateContext();
        Assert.Empty(await verify.AuditEntries.ToListAsync());
    }

    [Fact]
    public async Task RelockAsync_ClearsUnlockedUntilAndReason()
    {
        using var db = new SqliteTestDatabase();
        var yearId = await SeedYearAsync(db, SchoolYearStatus.Closed, "2025/26", new DateTime(2025, 8, 1), Now.AddHours(1), "correction");

        await CreateService(db).RelockAsync(yearId);

        var year = await LoadYearAsync(db, yearId);
        Assert.Null(year.UnlockedUntil);
        Assert.Null(year.UnlockReason);
        Assert.Equal(SchoolYearStatus.Closed, year.Status);
    }

    [Fact]
    public async Task RelockAsync_ChangesNothing_ForLockedYear()
    {
        using var db = new SqliteTestDatabase();
        var yearId = await SeedClosedYearAsync(db);

        await CreateService(db).RelockAsync(yearId);

        var year = await LoadYearAsync(db, yearId);
        Assert.Null(year.UnlockedUntil);
        Assert.Null(year.UnlockReason);
        Assert.Equal(SchoolYearStatus.Closed, year.Status);
    }

    [Fact]
    public async Task RelockAsync_Throws_ForUnknownYear()
    {
        using var db = new SqliteTestDatabase();

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).RelockAsync(999));
    }

    [Fact]
    public async Task GetLockStateAsync_ReturnsLockFields()
    {
        using var db = new SqliteTestDatabase();
        var until = Now.AddHours(3);
        var yearId = await SeedYearAsync(db, SchoolYearStatus.Closed, "2025/26", new DateTime(2025, 8, 1), until, "correction");

        var state = await CreateService(db).GetLockStateAsync(yearId);

        Assert.Equal(yearId, state.SchoolYearId);
        Assert.Equal("2025/26", state.Label);
        Assert.Equal(SchoolYearStatus.Closed, state.Status);
        Assert.Equal(new DateTime(2026, 8, 1), state.ClosedOn);
        Assert.Equal(until, state.UnlockedUntil);
        Assert.Equal("correction", state.UnlockReason);
    }

    [Fact]
    public async Task GetLockStateAsync_Throws_ForUnknownYear()
    {
        using var db = new SqliteTestDatabase();

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db).GetLockStateAsync(999));
    }

    [Fact]
    public async Task GetClosedYearsAsync_ReturnsOnlyClosedYears_NewestFirst()
    {
        using var db = new SqliteTestDatabase();
        var older = await SeedYearAsync(db, SchoolYearStatus.Closed, "2023/24", new DateTime(2023, 8, 1));
        var newer = await SeedYearAsync(db, SchoolYearStatus.Closed, "2024/25", new DateTime(2024, 8, 1));
        await SeedYearAsync(db, SchoolYearStatus.Active, "2025/26", new DateTime(2025, 8, 1));
        await SeedYearAsync(db, SchoolYearStatus.Planned, "2026/27", new DateTime(2026, 8, 1));

        var states = await CreateService(db).GetClosedYearsAsync();

        Assert.Equal([newer, older], states.Select(s => s.SchoolYearId));
        Assert.All(states, s => Assert.True(s.IsClosed));
    }

    [Fact]
    public async Task GetClosedYearsAsync_ReturnsEmpty_WhenNoYearIsClosed()
    {
        using var db = new SqliteTestDatabase();
        await SeedYearAsync(db, SchoolYearStatus.Active, "2025/26", new DateTime(2025, 8, 1));

        Assert.Empty(await CreateService(db).GetClosedYearsAsync());
    }
}
