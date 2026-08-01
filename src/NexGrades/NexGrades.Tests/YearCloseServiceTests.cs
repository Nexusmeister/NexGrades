using NexGrades.Data.Entities;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class YearCloseServiceTests
{
    [Fact]
    public async Task GetCloseReadinessAsync_Blocks_WhenAClassInstanceIsStillActive()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var year = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A", Status = ClassInstanceStatus.Active };
        context.AddRange(year, group, instance);
        await context.SaveChangesAsync();

        var service = new YearCloseService(context, new FixedClock(DateTime.Now));
        var readiness = await service.GetCloseReadinessAsync(year.Id);

        Assert.False(readiness.CanClose);
        Assert.Contains(readiness.BlockingIssues, issue => issue.Contains("5A"));
    }

    [Fact]
    public async Task GetCloseReadinessAsync_Warns_ButDoesNotBlock_WhenBucketWeightsDoNotSumTo100()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var year = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A", Status = ClassInstanceStatus.RolledForward };
        var subject = new Subject { Name = "Mathematik", ShortCode = "MA" };
        var subjectInstance = new SubjectInstance { ClassInstance = instance, Subject = subject };
        var bucket = new GradeBucket { SubjectInstance = subjectInstance, Name = "Schriftlich", Weight = 90m };
        context.AddRange(year, group, instance, subject, subjectInstance, bucket);
        await context.SaveChangesAsync();

        var service = new YearCloseService(context, new FixedClock(DateTime.Now));
        var readiness = await service.GetCloseReadinessAsync(year.Id);

        Assert.True(readiness.CanClose);
        Assert.Contains(readiness.Warnings, w => w.Contains("90"));
    }

    [Fact]
    public async Task CloseSchoolYearAsync_SetsClosedStatusAndTimestamp_OnceEveryClassIsDispositioned()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var year = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A", Status = ClassInstanceStatus.Archived };
        context.AddRange(year, group, instance);
        await context.SaveChangesAsync();

        var now = new DateTime(2026, 7, 20);
        var service = new YearCloseService(context, new FixedClock(now));
        await service.CloseSchoolYearAsync(year.Id);

        var refreshed = await context.SchoolYears.FindAsync(year.Id);
        Assert.NotNull(refreshed);
        Assert.Equal(SchoolYearStatus.Closed, refreshed!.Status);
        Assert.Equal(now, refreshed.ClosedOn);
    }

    [Fact]
    public async Task CloseSchoolYearAsync_Throws_WhenBlockingIssuesRemain()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var year = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A", Status = ClassInstanceStatus.Active };
        context.AddRange(year, group, instance);
        await context.SaveChangesAsync();

        var service = new YearCloseService(context, new FixedClock(DateTime.Now));

        await Assert.ThrowsAsync<Domain.Exceptions.InvariantViolationException>(() => service.CloseSchoolYearAsync(year.Id));
    }
}
