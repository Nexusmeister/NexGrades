using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;
using NexGrades.Tests.TestSupport;

namespace NexGrades.Tests.Services;

public class SchoolYearServiceYearEndReminderTests
{
    private static readonly DateTime EndDate = new(2026, 7, 31);

    private static SchoolYearService CreateService(SqliteTestDatabase db, DateTime now) =>
        new(db.CreateContextFactory(), new FixedClock(now), NullLogger<SchoolYearService>.Instance);

    private static async Task<int> SeedYearAsync(
        SqliteTestDatabase db, SchoolYearStatus status, string label = "2025/26", DateTime? start = null, DateTime? end = null)
    {
        await using var context = db.CreateContext();
        var year = new SchoolYear
        {
            Label = label,
            StartDate = start ?? new DateTime(2025, 8, 1),
            EndDate = end ?? EndDate,
            Status = status,
        };
        context.SchoolYears.Add(year);
        await context.SaveChangesAsync();
        return year.Id;
    }

    [Fact]
    public void IsYearEndReminderDue_ReturnsFalse_TwentyNineDaysBeforeTheEnd() =>
        Assert.False(SchoolYearService.IsYearEndReminderDue(EndDate, EndDate.AddDays(-29).AddHours(23).AddMinutes(59)));

    [Fact]
    public void IsYearEndReminderDue_ReturnsTrue_OnTheFirstDayOfTheWindow() =>
        Assert.True(SchoolYearService.IsYearEndReminderDue(EndDate, EndDate.AddDays(-28)));

    [Fact]
    public void IsYearEndReminderDue_IgnoresTheTimeOfDay_OnTheFirstDayOfTheWindow() =>
        Assert.True(SchoolYearService.IsYearEndReminderDue(EndDate.AddHours(18), EndDate.AddDays(-28).AddMinutes(1)));

    [Fact]
    public void IsYearEndReminderDue_ReturnsTrue_LongAfterTheEnd() =>
        Assert.True(SchoolYearService.IsYearEndReminderDue(EndDate, EndDate.AddDays(400)));

    [Fact]
    public async Task GetYearEndReminderAsync_ReturnsNull_TwentyNineDaysBeforeTheEnd()
    {
        using var db = new SqliteTestDatabase();
        await SeedYearAsync(db, SchoolYearStatus.Active);

        var reminder = await CreateService(db, EndDate.AddDays(-29).AddHours(23).AddMinutes(59)).GetYearEndReminderAsync();

        Assert.Null(reminder);
    }

    [Fact]
    public async Task GetYearEndReminderAsync_ReturnsReminder_OnTheFirstDayOfTheWindow()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedYearAsync(db, SchoolYearStatus.Active);

        var reminder = await CreateService(db, EndDate.AddDays(-28).AddMinutes(1)).GetYearEndReminderAsync();

        Assert.NotNull(reminder);
        Assert.Equal(id, reminder.SchoolYearId);
        Assert.Equal("2025/26", reminder.Label);
        Assert.Equal(EndDate, reminder.EndDate);
        Assert.False(reminder.HasEnded);
    }

    [Fact]
    public async Task GetYearEndReminderAsync_ReturnsReminderNotEnded_InsideTheWindow()
    {
        using var db = new SqliteTestDatabase();
        await SeedYearAsync(db, SchoolYearStatus.Active);

        var reminder = await CreateService(db, EndDate.AddDays(-10).AddHours(14)).GetYearEndReminderAsync();

        Assert.NotNull(reminder);
        Assert.False(reminder.HasEnded);
    }

    [Fact]
    public async Task GetYearEndReminderAsync_ReturnsReminderNotEnded_OnTheEndDateLateInTheDay()
    {
        using var db = new SqliteTestDatabase();
        await SeedYearAsync(db, SchoolYearStatus.Active);

        var reminder = await CreateService(db, EndDate.AddHours(23).AddMinutes(59)).GetYearEndReminderAsync();

        Assert.NotNull(reminder);
        Assert.False(reminder.HasEnded);
    }

    [Fact]
    public async Task GetYearEndReminderAsync_ReturnsReminderEnded_TheDayAfterTheEnd()
    {
        using var db = new SqliteTestDatabase();
        await SeedYearAsync(db, SchoolYearStatus.Active);

        var reminder = await CreateService(db, EndDate.AddDays(1).AddMinutes(1)).GetYearEndReminderAsync();

        Assert.NotNull(reminder);
        Assert.True(reminder.HasEnded);
    }

    [Fact]
    public async Task GetYearEndReminderAsync_ReturnsReminderEnded_MonthsAfterTheEndWhileStillActive()
    {
        using var db = new SqliteTestDatabase();
        await SeedYearAsync(db, SchoolYearStatus.Active);

        var reminder = await CreateService(db, EndDate.AddDays(150)).GetYearEndReminderAsync();

        Assert.NotNull(reminder);
        Assert.True(reminder.HasEnded);
    }

    [Fact]
    public async Task GetYearEndReminderAsync_ReturnsNull_WhenThereIsNoYear()
    {
        using var db = new SqliteTestDatabase();

        var reminder = await CreateService(db, EndDate).GetYearEndReminderAsync();

        Assert.Null(reminder);
    }

    [Theory]
    [InlineData(SchoolYearStatus.Planned)]
    [InlineData(SchoolYearStatus.Closed)]
    public async Task GetYearEndReminderAsync_ReturnsNull_WhenNoYearIsActive(SchoolYearStatus status)
    {
        using var db = new SqliteTestDatabase();
        await SeedYearAsync(db, status);

        var reminder = await CreateService(db, EndDate).GetYearEndReminderAsync();

        Assert.Null(reminder);
    }

    [Fact]
    public async Task GetYearEndReminderAsync_StillReturnsTheActiveYear_WhenAPlannedSuccessorExists()
    {
        using var db = new SqliteTestDatabase();
        var activeId = await SeedYearAsync(db, SchoolYearStatus.Active);
        await SeedYearAsync(db, SchoolYearStatus.Planned, "2026/27", new DateTime(2026, 8, 1), new DateTime(2027, 7, 31));

        var reminder = await CreateService(db, EndDate.AddDays(-5)).GetYearEndReminderAsync();

        Assert.NotNull(reminder);
        Assert.Equal(activeId, reminder.SchoolYearId);
        Assert.Equal("2025/26", reminder.Label);
    }

    [Fact]
    public async Task GetYearEndReminderAsync_RefersToTheNewYear_OrNull_AfterTheYearWasClosed()
    {
        using var db = new SqliteTestDatabase();
        var oldId = await SeedYearAsync(db, SchoolYearStatus.Active);
        var nextId = await SeedYearAsync(db, SchoolYearStatus.Planned, "2026/27", new DateTime(2026, 8, 1), new DateTime(2027, 7, 31));
        var now = EndDate.AddDays(2);
        var clock = new FixedClock(now);
        await new YearCloseService(db.CreateContextFactory(), clock, NullLogger<YearCloseService>.Instance)
            .CloseAndActivateNextYearAsync(oldId, nextId);
        var service = CreateService(db, now);

        Assert.Null(await service.GetYearEndReminderAsync());

        var late = CreateService(db, new DateTime(2027, 7, 20, 9, 0, 0));
        var reminder = await late.GetYearEndReminderAsync();
        Assert.NotNull(reminder);
        Assert.Equal(nextId, reminder.SchoolYearId);
        Assert.Equal("2026/27", reminder.Label);
        Assert.False(reminder.HasEnded);
    }
}
