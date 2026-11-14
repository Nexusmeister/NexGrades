using NexGrades.App.Core.YearLock;

namespace NexGrades.App.Tests;

public class UnlockDurationTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 10, 30, 0);

    [Fact]
    public void Resolve_ReturnsOneHourFromNow_ForOneHour() =>
        Assert.Equal(Now.AddHours(1), UnlockDuration.Resolve(UnlockDurationOption.OneHour, null, Now));

    [Fact]
    public void Resolve_ReturnsLastMomentOfToday_ForEndOfToday() =>
        Assert.Equal(new DateTime(2026, 9, 2).AddTicks(-1), UnlockDuration.Resolve(UnlockDurationOption.EndOfToday, null, Now));

    [Fact]
    public void Resolve_ReturnsSevenDaysFromNow_ForOneWeek() =>
        Assert.Equal(Now.AddDays(7), UnlockDuration.Resolve(UnlockDurationOption.OneWeek, null, Now));

    [Fact]
    public void Resolve_ReturnsLastMomentOfThePickedDay_ForCustomDate() =>
        Assert.Equal(
            new DateTime(2026, 9, 11).AddTicks(-1),
            UnlockDuration.Resolve(UnlockDurationOption.CustomDate, new DateTime(2026, 9, 10, 8, 0, 0), Now));

    [Fact]
    public void Resolve_ReturnsNull_ForCustomDateWithoutDate() =>
        Assert.Null(UnlockDuration.Resolve(UnlockDurationOption.CustomDate, null, Now));

    [Fact]
    public void Resolve_Throws_ForUnknownOption() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => UnlockDuration.Resolve((UnlockDurationOption)99, null, Now));

    [Fact]
    public void Resolve_EndOfToday_IsStillInTheFuture_At235930()
    {
        var late = new DateTime(2026, 9, 1, 23, 59, 30);

        var until = UnlockDuration.Resolve(UnlockDurationOption.EndOfToday, null, late);

        Assert.True(until > late);
    }
}
