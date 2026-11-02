using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class SchoolYearServiceTests
{
    [Fact]
    public async Task EnsureActiveYearAsync_CreatesActiveYear_WhenNoYearExists()
    {
        using var db = new SqliteTestDatabase();
        var service = CreateService(db, new DateTime(2025, 10, 15));

        await service.EnsureActiveYearAsync();

        await using var context = db.CreateContext();
        var year = await context.SchoolYears.AsNoTracking().SingleAsync();
        Assert.Equal(SchoolYearStatus.Active, year.Status);
        Assert.Equal("2025/26", year.Label);
        Assert.Equal(new DateTime(2025, 8, 1), year.StartDate);
        Assert.Equal(new DateTime(2026, 7, 31), year.EndDate);
    }

    [Fact]
    public async Task EnsureActiveYearAsync_UsesPreviousAugustAsStart_WhenBeforeAugust()
    {
        using var db = new SqliteTestDatabase();
        var service = CreateService(db, new DateTime(2026, 3, 15));

        await service.EnsureActiveYearAsync();

        await using var context = db.CreateContext();
        var year = await context.SchoolYears.AsNoTracking().SingleAsync();
        Assert.Equal("2025/26", year.Label);
        Assert.Equal(new DateTime(2025, 8, 1), year.StartDate);
        Assert.Equal(new DateTime(2026, 7, 31), year.EndDate);
    }

    [Fact]
    public async Task EnsureActiveYearAsync_StartsNewYear_OnFirstOfAugust()
    {
        using var db = new SqliteTestDatabase();
        var service = CreateService(db, new DateTime(2026, 8, 1));

        await service.EnsureActiveYearAsync();

        await using var context = db.CreateContext();
        var year = await context.SchoolYears.AsNoTracking().SingleAsync();
        Assert.Equal("2026/27", year.Label);
        Assert.Equal(new DateTime(2026, 8, 1), year.StartDate);
        Assert.Equal(new DateTime(2027, 7, 31), year.EndDate);
    }

    [Fact]
    public async Task EnsureActiveYearAsync_StaysInOldYear_OnThirtyFirstOfJuly()
    {
        using var db = new SqliteTestDatabase();
        var service = CreateService(db, new DateTime(2026, 7, 31, 23, 59, 59));

        await service.EnsureActiveYearAsync();

        await using var context = db.CreateContext();
        var year = await context.SchoolYears.AsNoTracking().SingleAsync();
        Assert.Equal("2025/26", year.Label);
        Assert.Equal(new DateTime(2026, 7, 31), year.EndDate);
    }

    [Fact]
    public async Task EnsureActiveYearAsync_UsesJanuaryInPreviousStartYear_OnFirstOfJanuary()
    {
        using var db = new SqliteTestDatabase();
        var service = CreateService(db, new DateTime(2026, 1, 1));

        await service.EnsureActiveYearAsync();

        await using var context = db.CreateContext();
        var year = await context.SchoolYears.AsNoTracking().SingleAsync();
        Assert.Equal("2025/26", year.Label);
    }

    [Fact]
    public async Task EnsureActiveYearAsync_UsesTwoDigitSuffix_AcrossCenturyBoundary()
    {
        using var db = new SqliteTestDatabase();
        var service = CreateService(db, new DateTime(2099, 9, 1));

        await service.EnsureActiveYearAsync();

        await using var context = db.CreateContext();
        var year = await context.SchoolYears.AsNoTracking().SingleAsync();
        Assert.Equal("2099/00", year.Label);
        Assert.Equal(new DateTime(2100, 7, 31), year.EndDate);
    }

    [Fact]
    public async Task EnsureActiveYearAsync_PadsSuffixWithZero_ForSingleDigitYearEnd()
    {
        using var db = new SqliteTestDatabase();
        var service = CreateService(db, new DateTime(2030, 9, 1));

        await service.EnsureActiveYearAsync();

        await using var context = db.CreateContext();
        var year = await context.SchoolYears.AsNoTracking().SingleAsync();
        Assert.Equal("2030/31", year.Label);
    }

    [Fact]
    public async Task EnsureActiveYearAsync_ChangesNothing_WhenActiveYearAlreadyExists()
    {
        using var db = new SqliteTestDatabase();
        var existingId = await SeedYearAsync(db, "2020/21", new DateTime(2020, 8, 1), new DateTime(2021, 7, 31), SchoolYearStatus.Active);
        var service = CreateService(db, new DateTime(2026, 3, 15));

        await service.EnsureActiveYearAsync();

        await using var context = db.CreateContext();
        var year = await context.SchoolYears.AsNoTracking().SingleAsync();
        Assert.Equal(existingId, year.Id);
        Assert.Equal("2020/21", year.Label);
        Assert.Equal(new DateTime(2020, 8, 1), year.StartDate);
        Assert.Equal(new DateTime(2021, 7, 31), year.EndDate);
        Assert.Equal(SchoolYearStatus.Active, year.Status);
    }

    [Fact]
    public async Task EnsureActiveYearAsync_CreatesActiveYear_WhenOnlyPlannedYearExists()
    {
        using var db = new SqliteTestDatabase();
        var plannedId = await SeedYearAsync(db, "2030/31", new DateTime(2030, 8, 1), new DateTime(2031, 7, 31), SchoolYearStatus.Planned);
        var service = CreateService(db, new DateTime(2026, 3, 15));

        await service.EnsureActiveYearAsync();

        await using var context = db.CreateContext();
        var years = await context.SchoolYears.AsNoTracking().ToListAsync();
        Assert.Equal(2, years.Count);
        var planned = Assert.Single(years, y => y.Id == plannedId);
        Assert.Equal(SchoolYearStatus.Planned, planned.Status);
        Assert.Equal("2030/31", planned.Label);
        var active = Assert.Single(years, y => y.Status == SchoolYearStatus.Active);
        Assert.Equal("2025/26", active.Label);
    }

    [Fact]
    public async Task EnsureActiveYearAsync_CreatesActiveYear_WhenOnlyClosedYearExists()
    {
        using var db = new SqliteTestDatabase();
        var closedId = await SeedYearAsync(db, "2024/25", new DateTime(2024, 8, 1), new DateTime(2025, 7, 31), SchoolYearStatus.Closed);
        var service = CreateService(db, new DateTime(2026, 3, 15));

        await service.EnsureActiveYearAsync();

        await using var context = db.CreateContext();
        var years = await context.SchoolYears.AsNoTracking().ToListAsync();
        Assert.Equal(2, years.Count);
        var closed = Assert.Single(years, y => y.Id == closedId);
        Assert.Equal(SchoolYearStatus.Closed, closed.Status);
        Assert.Equal("2024/25", closed.Label);
        var active = Assert.Single(years, y => y.Status == SchoolYearStatus.Active);
        Assert.Equal("2025/26", active.Label);
    }

    [Fact]
    public async Task EnsureActiveYearAsync_IsIdempotent_WhenCalledTwice()
    {
        using var db = new SqliteTestDatabase();
        var service = CreateService(db, new DateTime(2026, 3, 15));

        await service.EnsureActiveYearAsync();
        await using var first = db.CreateContext();
        var created = await first.SchoolYears.AsNoTracking().SingleAsync();

        await service.EnsureActiveYearAsync();

        await using var context = db.CreateContext();
        var year = await context.SchoolYears.AsNoTracking().SingleAsync();
        Assert.Equal(created.Id, year.Id);
        Assert.Equal("2025/26", year.Label);
    }

    private static SchoolYearService CreateService(SqliteTestDatabase db, DateTime now) =>
        new(db.CreateContextFactory(), new FixedClock(now), NullLogger<SchoolYearService>.Instance);

    private static async Task<int> SeedYearAsync(SqliteTestDatabase db, string label, DateTime start, DateTime end, SchoolYearStatus status)
    {
        await using var context = db.CreateContext();
        var year = new SchoolYear { Label = label, StartDate = start, EndDate = end, Status = status };
        context.SchoolYears.Add(year);
        await context.SaveChangesAsync();
        return year.Id;
    }
}
