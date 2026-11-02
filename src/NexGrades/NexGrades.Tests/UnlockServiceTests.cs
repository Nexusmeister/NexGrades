using NexGrades.Data.Entities;
using NexGrades.Domain.Auditing;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

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
        new(db.CreateContextFactory(), new FixedClock(Now), new UnlockReasonStore());

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
}
