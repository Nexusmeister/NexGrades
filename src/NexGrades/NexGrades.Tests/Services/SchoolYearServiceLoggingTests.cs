using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;
using NexGrades.Tests.TestSupport;

namespace NexGrades.Tests.Services;

public class SchoolYearServiceLoggingTests
{
    private static readonly DateTime Now = new(2025, 10, 15);

    [Fact]
    public async Task EnsureActiveYearAsync_LogsInformation_WhenItCreatesTheActiveYear()
    {
        using var db = new SqliteTestDatabase();
        var logger = new CapturingLogger<SchoolYearService>();

        await new SchoolYearService(db.CreateContextFactory(), new FixedClock(Now), logger).EnsureActiveYearAsync();

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message == "Created an Active school year");
    }

    [Fact]
    public async Task EnsureActiveYearAsync_LogsNoSchoolYearLabel_WhenItCreatesTheActiveYear()
    {
        using var db = new SqliteTestDatabase();
        var logger = new CapturingLogger<SchoolYearService>();

        await new SchoolYearService(db.CreateContextFactory(), new FixedClock(Now), logger).EnsureActiveYearAsync();

        await using var verify = db.CreateContext();
        var label = (await verify.SchoolYears.AsNoTracking().SingleAsync()).Label;
        Assert.NotEmpty(logger.Entries);
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains(label, StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, e => e.Exception is not null);
    }

    [Fact]
    public async Task UpdatePlannedYearAsync_LogsInformation_WithoutTheLabel()
    {
        using var db = new SqliteTestDatabase();
        int plannedId;
        await using (var seed = db.CreateContext())
        {
            var planned = new SchoolYear
            {
                Label = "2026/27",
                StartDate = new DateTime(2026, 8, 1),
                EndDate = new DateTime(2027, 7, 31),
                Status = SchoolYearStatus.Planned,
            };
            seed.SchoolYears.Add(new SchoolYear
            {
                Label = "2025/26",
                StartDate = new DateTime(2025, 8, 1),
                EndDate = new DateTime(2026, 7, 31),
                Status = SchoolYearStatus.Active,
            });
            seed.SchoolYears.Add(planned);
            await seed.SaveChangesAsync();
            plannedId = planned.Id;
        }

        var logger = new CapturingLogger<SchoolYearService>();
        var draft = new SchoolYearDraft("Secret Label", new DateTime(2026, 9, 1), new DateTime(2027, 7, 31));

        await new SchoolYearService(db.CreateContextFactory(), new FixedClock(Now), logger).UpdatePlannedYearAsync(plannedId, draft);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("Updated a Planned school year", entry.Message);
        Assert.DoesNotContain("Secret Label", entry.Message, StringComparison.Ordinal);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public async Task EnsureActiveYearAsync_LogsNothing_WhenAnActiveYearAlreadyExists()
    {
        using var db = new SqliteTestDatabase();
        await using (var seed = db.CreateContext())
        {
            seed.SchoolYears.Add(new SchoolYear
            {
                Label = "2025/26",
                StartDate = new DateTime(2025, 8, 1),
                EndDate = new DateTime(2026, 7, 31),
                Status = SchoolYearStatus.Active,
            });
            await seed.SaveChangesAsync();
        }

        var logger = new CapturingLogger<SchoolYearService>();

        await new SchoolYearService(db.CreateContextFactory(), new FixedClock(Now), logger).EnsureActiveYearAsync();

        Assert.Empty(logger.Entries);
        await using var verify = db.CreateContext();
        Assert.Equal(1, await verify.SchoolYears.CountAsync());
    }
}
