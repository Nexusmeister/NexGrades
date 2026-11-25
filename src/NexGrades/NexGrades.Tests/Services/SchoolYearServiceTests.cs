using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;
using NexGrades.Tests.TestSupport;

namespace NexGrades.Tests.Services;

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

    [Fact]
    public async Task GetYearsAsync_ReturnsAllYearsNewestFirst_AcrossStatuses()
    {
        using var db = new SqliteTestDatabase();
        var closedId = await SeedYearAsync(db, "2024/25", new DateTime(2024, 8, 1), new DateTime(2025, 7, 31), SchoolYearStatus.Closed);
        var plannedId = await SeedYearAsync(db, "2026/27", new DateTime(2026, 8, 1), new DateTime(2027, 7, 31), SchoolYearStatus.Planned);
        var activeId = await SeedYearAsync(db, "2025/26", new DateTime(2025, 8, 1), new DateTime(2026, 7, 31), SchoolYearStatus.Active);
        var service = CreateService(db, new DateTime(2026, 3, 15));

        var years = await service.GetYearsAsync();

        Assert.Equal([plannedId, activeId, closedId], years.Select(y => y.Id));
        Assert.Equal([SchoolYearStatus.Planned, SchoolYearStatus.Active, SchoolYearStatus.Closed], years.Select(y => y.Status));
        Assert.Equal("2025/26", years[1].Label);
        Assert.Equal(new DateTime(2025, 8, 1), years[1].StartDate);
        Assert.Equal(new DateTime(2026, 7, 31), years[1].EndDate);
    }

    [Fact]
    public async Task GetYearsAsync_ReturnsEmptyList_WhenNoYearExists()
    {
        using var db = new SqliteTestDatabase();
        var service = CreateService(db, new DateTime(2026, 3, 15));

        var years = await service.GetYearsAsync();

        Assert.Empty(years);
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

    private static readonly DateTime Aug2025 = new(2025, 8, 1);
    private static readonly DateTime Jul2026 = new(2026, 7, 31);

    private static Task<int> SeedActiveYearAsync(SqliteTestDatabase db) =>
        SeedYearAsync(db, "2025/26", Aug2025, Jul2026, SchoolYearStatus.Active);

    [Fact]
    public async Task ProposeNextYearAsync_ReturnsFollowingYear_ForNormalYear()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedActiveYearAsync(db);

        var draft = await CreateService(db, Aug2025).ProposeNextYearAsync(id);

        Assert.Equal("2026/27", draft.Label);
        Assert.Equal(new DateTime(2026, 8, 1), draft.StartDate);
        Assert.Equal(new DateTime(2027, 7, 31), draft.EndDate);
    }

    [Fact]
    public async Task ProposeNextYearAsync_UsesTwoDigitSuffix_AcrossCenturyBoundary()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedYearAsync(db, "2098/99", new DateTime(2098, 8, 1), new DateTime(2099, 7, 31), SchoolYearStatus.Active);

        var draft = await CreateService(db, Aug2025).ProposeNextYearAsync(id);

        Assert.Equal("2099/00", draft.Label);
        Assert.Equal(new DateTime(2100, 7, 31), draft.EndDate);
    }

    [Fact]
    public async Task ProposeNextYearAsync_DoesNotPersistAnything()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedActiveYearAsync(db);

        await CreateService(db, Aug2025).ProposeNextYearAsync(id);

        await AssertYearCountAsync(db, 1);
    }

    [Fact]
    public async Task ProposeNextYearAsync_Throws_ForMissingYear()
    {
        using var db = new SqliteTestDatabase();

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).ProposeNextYearAsync(4711));
    }

    [Fact]
    public async Task CreateNextYearAsync_CreatesPlannedYear_WithTrimmedLabelAndGivenDates()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedActiveYearAsync(db);
        var draft = new SchoolYearDraft("  2026/27  ", new DateTime(2026, 8, 1), new DateTime(2027, 7, 31));

        var created = await CreateService(db, Aug2025).CreateNextYearAsync(id, draft);

        Assert.Equal("2026/27", created.Label);
        Assert.Equal(SchoolYearStatus.Planned, created.Status);
        await using var context = db.CreateContext();
        var stored = await context.SchoolYears.AsNoTracking().SingleAsync(sy => sy.Id == created.Id);
        Assert.Equal("2026/27", stored.Label);
        Assert.Equal(SchoolYearStatus.Planned, stored.Status);
        Assert.Equal(new DateTime(2026, 8, 1), stored.StartDate);
        Assert.Equal(new DateTime(2027, 7, 31), stored.EndDate);
    }

    [Fact]
    public async Task CreateNextYearAsync_KeepsOldYearActive()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedActiveYearAsync(db);
        var service = CreateService(db, Aug2025);

        await service.CreateNextYearAsync(id, await service.ProposeNextYearAsync(id));

        await using var context = db.CreateContext();
        var old = await context.SchoolYears.AsNoTracking().SingleAsync(sy => sy.Id == id);
        Assert.Equal(SchoolYearStatus.Active, old.Status);
    }

    [Theory]
    [InlineData(SchoolYearStatus.Planned)]
    [InlineData(SchoolYearStatus.Closed)]
    public async Task CreateNextYearAsync_Throws_WhenPreviousYearIsNotActive(SchoolYearStatus status)
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedYearAsync(db, "2025/26", Aug2025, Jul2026, status);
        var draft = new SchoolYearDraft("2026/27", new DateTime(2026, 8, 1), new DateTime(2027, 7, 31));

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).CreateNextYearAsync(id, draft));

        await AssertYearCountAsync(db, 1);
    }

    [Fact]
    public async Task CreateNextYearAsync_Throws_WhenPreviousYearIsMissing()
    {
        using var db = new SqliteTestDatabase();
        var draft = new SchoolYearDraft("2026/27", new DateTime(2026, 8, 1), new DateTime(2027, 7, 31));

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).CreateNextYearAsync(4711, draft));
    }

    [Fact]
    public async Task CreateNextYearAsync_Throws_WhenPlannedSuccessorAlreadyExists()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedActiveYearAsync(db);
        await SeedYearAsync(db, "2026/27", new DateTime(2026, 8, 1), new DateTime(2027, 7, 31), SchoolYearStatus.Planned);
        var draft = new SchoolYearDraft("2027/28", new DateTime(2027, 8, 1), new DateTime(2028, 7, 31));

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).CreateNextYearAsync(id, draft));

        await AssertYearCountAsync(db, 2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateNextYearAsync_Throws_ForEmptyLabel(string label)
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedActiveYearAsync(db);
        var draft = new SchoolYearDraft(label, new DateTime(2026, 8, 1), new DateTime(2027, 7, 31));

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).CreateNextYearAsync(id, draft));

        await AssertYearCountAsync(db, 1);
    }

    [Fact]
    public async Task CreateNextYearAsync_Throws_WhenLabelIsUsedIgnoringCase()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedActiveYearAsync(db);
        await SeedYearAsync(db, "Year X", new DateTime(2020, 8, 1), new DateTime(2021, 7, 31), SchoolYearStatus.Closed);
        var draft = new SchoolYearDraft("year x", new DateTime(2026, 8, 1), new DateTime(2027, 7, 31));

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).CreateNextYearAsync(id, draft));

        await AssertYearCountAsync(db, 2);
    }

    [Fact]
    public async Task CreateNextYearAsync_Throws_WhenStartIsAfterEnd()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedActiveYearAsync(db);
        var draft = new SchoolYearDraft("2026/27", new DateTime(2027, 7, 31), new DateTime(2026, 8, 1));

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).CreateNextYearAsync(id, draft));

        await AssertYearCountAsync(db, 1);
    }

    [Fact]
    public async Task CreateNextYearAsync_Throws_WhenStartEqualsEnd()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedActiveYearAsync(db);
        var draft = new SchoolYearDraft("2026/27", new DateTime(2026, 8, 1), new DateTime(2026, 8, 1));

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).CreateNextYearAsync(id, draft));

        await AssertYearCountAsync(db, 1);
    }

    [Fact]
    public async Task CreateNextYearAsync_Throws_WhenDatesOverlapExistingYear()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedActiveYearAsync(db);
        var draft = new SchoolYearDraft("2026/27", Jul2026, new DateTime(2027, 7, 31));

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).CreateNextYearAsync(id, draft));

        await AssertYearCountAsync(db, 1);
    }

    [Theory]
    [InlineData("2016-08-01", "2017-07-31")]
    [InlineData("2026-07-31", "2027-07-31")]
    public async Task CreateNextYearAsync_Throws_WhenStartIsNotAfterPreviousYearEnd(string start, string end)
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedActiveYearAsync(db);
        var draft = new SchoolYearDraft("2026/27", DateTime.Parse(start, System.Globalization.CultureInfo.InvariantCulture), DateTime.Parse(end, System.Globalization.CultureInfo.InvariantCulture));

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).CreateNextYearAsync(id, draft));

        await AssertYearCountAsync(db, 1);
    }

    [Fact]
    public async Task CreateNextYearAsync_Succeeds_WhenStartingTheDayAfterTheOldYearEnds()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedActiveYearAsync(db);
        var draft = new SchoolYearDraft("2026/27", Jul2026.AddDays(1), new DateTime(2027, 7, 31));

        var created = await CreateService(db, Aug2025).CreateNextYearAsync(id, draft);

        Assert.Equal(SchoolYearStatus.Planned, created.Status);
    }

    [Fact]
    public async Task FindPlannedSuccessorAsync_ReturnsNull_WhenNoPlannedYearExists()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedActiveYearAsync(db);

        Assert.Null(await CreateService(db, Aug2025).FindPlannedSuccessorAsync(id));
    }

    [Fact]
    public async Task FindPlannedSuccessorAsync_ReturnsEarliestPlannedYearAfter()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedActiveYearAsync(db);
        await SeedYearAsync(db, "2027/28", new DateTime(2027, 8, 1), new DateTime(2028, 7, 31), SchoolYearStatus.Planned);
        var earliest = await SeedYearAsync(db, "2026/27", new DateTime(2026, 8, 1), new DateTime(2027, 7, 31), SchoolYearStatus.Planned);

        var found = await CreateService(db, Aug2025).FindPlannedSuccessorAsync(id);

        Assert.NotNull(found);
        Assert.Equal(earliest, found.Id);
        Assert.Equal("2026/27", found.Label);
    }

    [Fact]
    public async Task FindPlannedSuccessorAsync_ReturnsNull_WhenOnlyEarlierPlannedYearExists()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedActiveYearAsync(db);
        await SeedYearAsync(db, "2024/25", new DateTime(2024, 8, 1), new DateTime(2025, 7, 31), SchoolYearStatus.Planned);

        Assert.Null(await CreateService(db, Aug2025).FindPlannedSuccessorAsync(id));
    }

    [Fact]
    public async Task FindPlannedSuccessorAsync_ReturnsNull_WhenLaterYearIsNotPlanned()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedActiveYearAsync(db);
        await SeedYearAsync(db, "2026/27", new DateTime(2026, 8, 1), new DateTime(2027, 7, 31), SchoolYearStatus.Closed);

        Assert.Null(await CreateService(db, Aug2025).FindPlannedSuccessorAsync(id));
    }

    [Fact]
    public async Task FindPlannedSuccessorAsync_Throws_ForMissingYear()
    {
        using var db = new SqliteTestDatabase();

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).FindPlannedSuccessorAsync(4711));
    }

    [Fact]
    public async Task GetActiveYearAsync_ReturnsActiveYear_WhenOneExists()
    {
        using var db = new SqliteTestDatabase();
        await SeedYearAsync(db, "2024/25", new DateTime(2024, 8, 1), new DateTime(2025, 7, 31), SchoolYearStatus.Closed);
        var id = await SeedActiveYearAsync(db);

        var active = await CreateService(db, Aug2025).GetActiveYearAsync();

        Assert.NotNull(active);
        Assert.Equal(id, active.Id);
        Assert.Equal("2025/26", active.Label);
        Assert.Equal(SchoolYearStatus.Active, active.Status);
    }

    [Fact]
    public async Task GetActiveYearAsync_ReturnsNull_WhenNoYearIsActive()
    {
        using var db = new SqliteTestDatabase();
        await SeedYearAsync(db, "2026/27", new DateTime(2026, 8, 1), new DateTime(2027, 7, 31), SchoolYearStatus.Planned);

        Assert.Null(await CreateService(db, Aug2025).GetActiveYearAsync());
    }

    private static readonly DateTime Aug2026 = new(2026, 8, 1);
    private static readonly DateTime Jul2027 = new(2027, 7, 31);

    private sealed record ActiveAndPlanned(int ActiveId, int PlannedId);

    /// <summary>Seeds the Active year 2025/26 and its Planned successor 2026/27.</summary>
    private static async Task<ActiveAndPlanned> SeedActiveAndPlannedAsync(SqliteTestDatabase db)
    {
        var activeId = await SeedActiveYearAsync(db);
        var plannedId = await SeedYearAsync(db, "2026/27", Aug2026, Jul2027, SchoolYearStatus.Planned);
        return new ActiveAndPlanned(activeId, plannedId);
    }

    private static async Task AssertYearAsync(
        SqliteTestDatabase db, int id, string label, DateTime start, DateTime end, SchoolYearStatus status)
    {
        await using var context = db.CreateContext();
        var year = await context.SchoolYears.AsNoTracking().SingleAsync(sy => sy.Id == id);
        Assert.Equal(label, year.Label);
        Assert.Equal(start, year.StartDate);
        Assert.Equal(end, year.EndDate);
        Assert.Equal(status, year.Status);
    }

    private static Task AssertPlannedUnchangedAsync(SqliteTestDatabase db, int plannedId) =>
        AssertYearAsync(db, plannedId, "2026/27", Aug2026, Jul2027, SchoolYearStatus.Planned);

    [Fact]
    public async Task UpdatePlannedYearAsync_SavesTrimmedLabelAndDates_ForPlannedYear()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedActiveAndPlannedAsync(db);
        var draft = new SchoolYearDraft("  Year Two  ", new DateTime(2026, 9, 1), new DateTime(2027, 6, 30));

        var updated = await CreateService(db, Aug2025).UpdatePlannedYearAsync(years.PlannedId, draft);

        Assert.Equal("Year Two", updated.Label);
        Assert.Equal(new DateTime(2026, 9, 1), updated.StartDate);
        Assert.Equal(new DateTime(2027, 6, 30), updated.EndDate);
        await AssertYearAsync(db, years.PlannedId, "Year Two", new DateTime(2026, 9, 1), new DateTime(2027, 6, 30), SchoolYearStatus.Planned);
    }

    [Fact]
    public async Task UpdatePlannedYearAsync_Succeeds_WhenLabelAndDatesAreUnchanged()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedActiveAndPlannedAsync(db);

        await CreateService(db, Aug2025).UpdatePlannedYearAsync(years.PlannedId, new SchoolYearDraft("2026/27", Aug2026, Jul2027));

        await AssertPlannedUnchangedAsync(db, years.PlannedId);
    }

    [Fact]
    public async Task UpdatePlannedYearAsync_Succeeds_WhenOnlyTheCaseOfItsOwnLabelChanges()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedActiveAndPlannedAsync(db);
        await using (var context = db.CreateContext())
        {
            (await context.SchoolYears.SingleAsync(sy => sy.Id == years.PlannedId)).Label = "Year Two";
            await context.SaveChangesAsync();
        }

        await CreateService(db, Aug2025).UpdatePlannedYearAsync(years.PlannedId, new SchoolYearDraft("YEAR TWO", Aug2026, Jul2027));

        await AssertYearAsync(db, years.PlannedId, "YEAR TWO", Aug2026, Jul2027, SchoolYearStatus.Planned);
    }

    [Fact]
    public async Task UpdatePlannedYearAsync_Succeeds_WhenNewRangeOverlapsOnlyItsOwnOldRange()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedActiveAndPlannedAsync(db);
        var draft = new SchoolYearDraft("2026/27", new DateTime(2026, 9, 1), new DateTime(2027, 8, 31));

        await CreateService(db, Aug2025).UpdatePlannedYearAsync(years.PlannedId, draft);

        await AssertYearAsync(db, years.PlannedId, "2026/27", new DateTime(2026, 9, 1), new DateTime(2027, 8, 31), SchoolYearStatus.Planned);
    }

    [Fact]
    public async Task UpdatePlannedYearAsync_Succeeds_WhenStartingTheDayAfterTheActiveYearEnds()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedActiveAndPlannedAsync(db);
        var draft = new SchoolYearDraft("2026/27", Jul2026.AddDays(1), Jul2027);

        await CreateService(db, Aug2025).UpdatePlannedYearAsync(years.PlannedId, draft);

        await AssertYearAsync(db, years.PlannedId, "2026/27", Jul2026.AddDays(1), Jul2027, SchoolYearStatus.Planned);
    }

    [Fact]
    public async Task UpdatePlannedYearAsync_KeepsIdAndPlannedStatus_AndFindPlannedSuccessorStillReturnsIt()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedActiveAndPlannedAsync(db);
        var service = CreateService(db, Aug2025);

        var updated = await service.UpdatePlannedYearAsync(years.PlannedId, new SchoolYearDraft("Renamed", new DateTime(2026, 9, 1), Jul2027));

        Assert.Equal(years.PlannedId, updated.Id);
        Assert.Equal(SchoolYearStatus.Planned, updated.Status);
        var found = await service.FindPlannedSuccessorAsync(years.ActiveId);
        Assert.NotNull(found);
        Assert.Equal(years.PlannedId, found.Id);
        Assert.Equal("Renamed", found.Label);
        Assert.Equal(new DateTime(2026, 9, 1), found.StartDate);
        await AssertYearCountAsync(db, 2);
    }

    [Fact]
    public async Task UpdatePlannedYearAsync_LeavesOtherYearsUnchanged()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedActiveAndPlannedAsync(db);
        var closedId = await SeedYearAsync(db, "2024/25", new DateTime(2024, 8, 1), new DateTime(2025, 7, 31), SchoolYearStatus.Closed);

        await CreateService(db, Aug2025).UpdatePlannedYearAsync(years.PlannedId, new SchoolYearDraft("Renamed", new DateTime(2026, 9, 1), Jul2027));

        await AssertYearAsync(db, years.ActiveId, "2025/26", Aug2025, Jul2026, SchoolYearStatus.Active);
        await AssertYearAsync(db, closedId, "2024/25", new DateTime(2024, 8, 1), new DateTime(2025, 7, 31), SchoolYearStatus.Closed);
    }

    [Fact]
    public async Task UpdatePlannedYearAsync_WritesNoAuditEntry()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedActiveAndPlannedAsync(db);

        await CreateService(db, Aug2025).UpdatePlannedYearAsync(years.PlannedId, new SchoolYearDraft("Renamed", new DateTime(2026, 9, 1), Jul2027));

        await using var context = db.CreateContext();
        Assert.Empty(await context.AuditEntries.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task UpdatePlannedYearAsync_Succeeds_WhenGradesAndAssessmentsStayInsideNewRange()
    {
        using var db = new SqliteTestDatabase();
        await SeedYearAsync(db, "2024/25", new DateTime(2024, 8, 1), new DateTime(2025, 7, 31), SchoolYearStatus.Active);
        var seed = await GradeSheetSeed.CreateAsync(db, SchoolYearStatus.Planned);
        var student = await seed.AddStudentAsync("Pat", "Test");
        var newStart = new DateTime(2025, 9, 1);
        var newEnd = new DateTime(2026, 6, 30);
        var assessmentId = await seed.AddAssessmentAsync("Test A", DateOnly.FromDateTime(newStart));
        await seed.AddGradeAsync(student, 2m, DateOnly.FromDateTime(newEnd), assessmentId);
        await seed.AddGradeAsync(student, 3m, new DateOnly(2026, 1, 10), voidedOn: new DateTime(2026, 1, 11));

        await CreateService(db, Aug2025).UpdatePlannedYearAsync(seed.YearId, new SchoolYearDraft("2025/26", newStart, newEnd));

        await AssertYearAsync(db, seed.YearId, "2025/26", newStart, newEnd, SchoolYearStatus.Planned);
    }

    [Fact]
    public async Task UpdatePlannedYearAsync_Throws_ForMissingYear()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedActiveAndPlannedAsync(db);
        var draft = new SchoolYearDraft("2027/28", new DateTime(2027, 8, 1), new DateTime(2028, 7, 31));

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).UpdatePlannedYearAsync(4711, draft));

        await AssertYearCountAsync(db, 2);
        await AssertPlannedUnchangedAsync(db, years.PlannedId);
    }

    [Theory]
    [InlineData(SchoolYearStatus.Active)]
    [InlineData(SchoolYearStatus.Closed)]
    public async Task UpdatePlannedYearAsync_Throws_WhenYearIsNotPlanned(SchoolYearStatus status)
    {
        using var db = new SqliteTestDatabase();
        var activeId = await SeedActiveYearAsync(db);
        var targetId = activeId;
        var label = "2025/26";
        var start = Aug2025;
        var end = Jul2026;
        if (status == SchoolYearStatus.Closed)
        {
            label = "2024/25";
            start = new DateTime(2024, 8, 1);
            end = new DateTime(2025, 7, 31);
            targetId = await SeedYearAsync(db, label, start, end, SchoolYearStatus.Closed);
        }

        var draft = new SchoolYearDraft("Renamed", Aug2026, Jul2027);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).UpdatePlannedYearAsync(targetId, draft));

        await AssertYearAsync(db, targetId, label, start, end, status);
    }

    [Fact]
    public async Task UpdatePlannedYearAsync_Throws_ForClosedYear_EvenWhenUnlocked()
    {
        using var db = new SqliteTestDatabase();
        await SeedActiveYearAsync(db);
        int closedId;
        await using (var context = db.CreateContext())
        {
            var closed = new SchoolYear
            {
                Label = "2024/25",
                StartDate = new DateTime(2024, 8, 1),
                EndDate = new DateTime(2025, 7, 31),
                Status = SchoolYearStatus.Closed,
                UnlockedUntil = new DateTime(2030, 1, 1),
                UnlockReason = "Correction",
            };
            context.SchoolYears.Add(closed);
            await context.SaveChangesAsync();
            closedId = closed.Id;
        }

        var draft = new SchoolYearDraft("Renamed", Aug2026, Jul2027);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).UpdatePlannedYearAsync(closedId, draft));

        await AssertYearAsync(db, closedId, "2024/25", new DateTime(2024, 8, 1), new DateTime(2025, 7, 31), SchoolYearStatus.Closed);
        await using var verify = db.CreateContext();
        Assert.Empty(await verify.AuditEntries.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdatePlannedYearAsync_Throws_ForEmptyLabel(string label)
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedActiveAndPlannedAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db, Aug2025).UpdatePlannedYearAsync(years.PlannedId, new SchoolYearDraft(label, Aug2026, Jul2027)));

        await AssertPlannedUnchangedAsync(db, years.PlannedId);
    }

    [Fact]
    public async Task UpdatePlannedYearAsync_Throws_WhenLabelIsUsedByAnotherYearIgnoringCase()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedActiveAndPlannedAsync(db);
        await SeedYearAsync(db, "Year X", new DateTime(2020, 8, 1), new DateTime(2021, 7, 31), SchoolYearStatus.Closed);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db, Aug2025).UpdatePlannedYearAsync(years.PlannedId, new SchoolYearDraft(" year x ", Aug2026, Jul2027)));

        await AssertPlannedUnchangedAsync(db, years.PlannedId);
    }

    [Fact]
    public async Task UpdatePlannedYearAsync_Throws_WhenStartIsAfterEnd()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedActiveAndPlannedAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db, Aug2025).UpdatePlannedYearAsync(years.PlannedId, new SchoolYearDraft("2026/27", Jul2027, Aug2026)));

        await AssertPlannedUnchangedAsync(db, years.PlannedId);
    }

    [Fact]
    public async Task UpdatePlannedYearAsync_Throws_WhenStartEqualsEnd()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedActiveAndPlannedAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db, Aug2025).UpdatePlannedYearAsync(years.PlannedId, new SchoolYearDraft("2026/27", Aug2026, Aug2026)));

        await AssertPlannedUnchangedAsync(db, years.PlannedId);
    }

    [Theory]
    [InlineData("2026-07-31", "2027-07-31")]
    [InlineData("2025-08-01", "2026-05-31")]
    public async Task UpdatePlannedYearAsync_Throws_WhenStartIsNotAfterActiveYearEnd(string start, string end)
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedActiveAndPlannedAsync(db);
        var draft = new SchoolYearDraft(
            "2026/27",
            DateTime.Parse(start, System.Globalization.CultureInfo.InvariantCulture),
            DateTime.Parse(end, System.Globalization.CultureInfo.InvariantCulture));

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).UpdatePlannedYearAsync(years.PlannedId, draft));

        await AssertPlannedUnchangedAsync(db, years.PlannedId);
    }

    [Fact]
    public async Task UpdatePlannedYearAsync_Throws_WhenDatesOverlapAnotherYear()
    {
        using var db = new SqliteTestDatabase();
        var years = await SeedActiveAndPlannedAsync(db);
        await SeedYearAsync(db, "2028/29", new DateTime(2028, 8, 1), new DateTime(2029, 7, 31), SchoolYearStatus.Closed);
        var draft = new SchoolYearDraft("2026/27", Aug2026, new DateTime(2028, 8, 1));

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).UpdatePlannedYearAsync(years.PlannedId, draft));

        await AssertPlannedUnchangedAsync(db, years.PlannedId);
    }

    [Fact]
    public async Task UpdatePlannedYearAsync_Throws_WhenNoActiveYearExists()
    {
        using var db = new SqliteTestDatabase();
        var plannedId = await SeedYearAsync(db, "2026/27", Aug2026, Jul2027, SchoolYearStatus.Planned);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db, Aug2025).UpdatePlannedYearAsync(plannedId, new SchoolYearDraft("Renamed", Aug2026, Jul2027)));

        await AssertPlannedUnchangedAsync(db, plannedId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdatePlannedYearAsync_Throws_WhenAnAssessmentWouldFallOutsideTheNewRange(bool voided)
    {
        using var db = new SqliteTestDatabase();
        await SeedYearAsync(db, "2024/25", new DateTime(2024, 8, 1), new DateTime(2025, 7, 31), SchoolYearStatus.Active);
        var seed = await GradeSheetSeed.CreateAsync(db, SchoolYearStatus.Planned);
        await seed.AddAssessmentAsync("Test A", new DateOnly(2026, 7, 15), voidedOn: voided ? new DateTime(2026, 7, 16) : null);
        var draft = new SchoolYearDraft("2025/26", GradeSheetSeed.YearStart, new DateTime(2026, 6, 30));

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).UpdatePlannedYearAsync(seed.YearId, draft));

        await AssertYearAsync(db, seed.YearId, "2025/26", GradeSheetSeed.YearStart, GradeSheetSeed.YearEnd, SchoolYearStatus.Planned);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdatePlannedYearAsync_Throws_WhenAGradeWouldFallOutsideTheNewRange(bool voided)
    {
        using var db = new SqliteTestDatabase();
        await SeedYearAsync(db, "2024/25", new DateTime(2024, 8, 1), new DateTime(2025, 7, 31), SchoolYearStatus.Active);
        var seed = await GradeSheetSeed.CreateAsync(db, SchoolYearStatus.Planned);
        var student = await seed.AddStudentAsync("Pat", "Test");
        await seed.AddGradeAsync(student, 2m, new DateOnly(2025, 8, 15), voidedOn: voided ? new DateTime(2025, 8, 16) : null);
        var draft = new SchoolYearDraft("2025/26", new DateTime(2025, 9, 1), GradeSheetSeed.YearEnd);

        await Assert.ThrowsAsync<InvariantViolationException>(() => CreateService(db, Aug2025).UpdatePlannedYearAsync(seed.YearId, draft));

        await AssertYearAsync(db, seed.YearId, "2025/26", GradeSheetSeed.YearStart, GradeSheetSeed.YearEnd, SchoolYearStatus.Planned);
    }

    private static async Task AssertYearCountAsync(SqliteTestDatabase db, int expected)
    {
        await using var context = db.CreateContext();
        Assert.Equal(expected, await context.SchoolYears.CountAsync());
    }
}
