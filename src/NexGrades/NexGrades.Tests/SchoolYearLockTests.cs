using NexGrades.Data.Entities;
using NexGrades.Domain.Auditing;

namespace NexGrades.Tests;

public class SchoolYearLockTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0);

    private static SchoolYear Year(SchoolYearStatus status, DateTime? unlockedUntil = null) => new()
    {
        Label = "2025/26",
        StartDate = new DateTime(2025, 8, 1),
        EndDate = new DateTime(2026, 7, 31),
        Status = status,
        UnlockedUntil = unlockedUntil,
    };

    [Theory]
    [InlineData(SchoolYearStatus.Active)]
    [InlineData(SchoolYearStatus.Planned)]
    public void IsWritable_ReturnsTrue_ForActiveAndPlannedYears(SchoolYearStatus status)
    {
        Assert.True(SchoolYearLock.IsWritable(Year(status), Now));
    }

    [Fact]
    public void IsWritable_ReturnsFalse_ForLockedClosedYear()
    {
        Assert.False(SchoolYearLock.IsWritable(Year(SchoolYearStatus.Closed), Now));
    }

    [Fact]
    public void IsWritable_ReturnsTrue_ForClosedYearUnlockedInFuture()
    {
        Assert.True(SchoolYearLock.IsWritable(Year(SchoolYearStatus.Closed, Now.AddTicks(1)), Now));
    }

    [Fact]
    public void IsWritable_ReturnsFalse_WhenUnlockExpiresExactlyNow()
    {
        Assert.False(SchoolYearLock.IsWritable(Year(SchoolYearStatus.Closed, Now), Now));
    }
}
