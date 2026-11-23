using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using NexGrades.Data.Entities;
using NexGrades.Domain.Auditing;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;
using NexGrades.Tests.TestSupport;

namespace NexGrades.Tests.Services;

/// <summary>
/// Assessments and the grades that belong to them are locked and audited with their school year. Also covers
/// both <c>SchoolYearResolver</c> paths for <see cref="Assessment"/> (it is internal): <c>SaveChanges</c> goes
/// through the synchronous resolver and <c>SaveChangesAsync</c> through the asynchronous one.
/// </summary>
public class AssessmentClosedYearTests
{
    private static readonly DateOnly Day = new(2025, 9, 15);

    private static readonly FixedClock Clock = new(new DateTime(2026, 8, 15, 10, 0, 0));

    private static readonly DateTime UnlockedUntil = new(2026, 8, 16);

    private sealed record Closed(SqliteTestDatabase Db, GradeSheetSeed Seed, int StudentId, int OtherStudentId, int AssessmentId, int GradeId);

    private static async Task<Closed> SeedClosedAsync(bool unlocked)
    {
        var db = new SqliteTestDatabase(new ClosedYearAuditInterceptor(Clock));
        try
        {
            var seed = await GradeSheetSeed.CreateAsync(db);
            var student = await seed.AddStudentAsync("Test", "Alpha");
            var other = await seed.AddStudentAsync("Test", "Beta");
            var assessmentId = await seed.AddAssessmentAsync("Test one", Day);
            var gradeId = await seed.AddGradeAsync(student, 2.0m, Day, assessmentId);
            await seed.SetYearStatusAsync(
                SchoolYearStatus.Closed,
                unlocked ? UnlockedUntil : null,
                unlocked ? "Corrected a transcription error" : null);
            return new Closed(db, seed, student, other, assessmentId, gradeId);
        }
        catch
        {
            // The caller owns the database only once seeding succeeded.
            db.Dispose();
            throw;
        }
    }

    private static AssessmentService CreateAssessmentService(Closed c) => new(c.Db.CreateContextFactory(), Clock);

    private static GradeService CreateGradeService(Closed c) => new(c.Db.CreateContextFactory(), Clock);

    private static async Task AssertAuditedAsync(Closed c, string entityType)
    {
        await using var verify = c.Db.CreateContext();
        var entries = await verify.AuditEntries.Where(a => a.EntityType == entityType).ToListAsync();
        Assert.NotEmpty(entries);
        Assert.All(entries, a => Assert.Equal("Corrected a transcription error", a.Reason));
    }

    private static async Task AssertNothingAuditedAsync(Closed c)
    {
        await using var verify = c.Db.CreateContext();
        Assert.Empty(await verify.AuditEntries.ToListAsync());
    }

    // Resolver, sync and async paths

    [Fact]
    [SuppressMessage("Usage", "VSTHRD103:Call async methods when in an async method", Justification = "Exercises the synchronous SaveChanges path, which uses the sync SchoolYearResolver of the interceptor.")]
    public async Task SaveChanges_Throws_ClosedYearException_ForANewAssessmentInALockedYear()
    {
        var c = await SeedClosedAsync(unlocked: false);
        using var _ = c.Db;

        await using var context = c.Db.CreateContext();
        context.Assessments.Add(new Assessment { GradeBucketId = c.Seed.BucketId, Title = "Test two", Date = Day });

        Assert.Throws<ClosedYearException>(() => context.SaveChanges());

        await using var verify = c.Db.CreateContext();
        Assert.Equal(1, await verify.Assessments.CountAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_Throws_ClosedYearException_ForANewAssessmentInALockedYear()
    {
        var c = await SeedClosedAsync(unlocked: false);
        using var _ = c.Db;

        await using var context = c.Db.CreateContext();
        context.Assessments.Add(new Assessment { GradeBucketId = c.Seed.BucketId, Title = "Test two", Date = Day });

        await Assert.ThrowsAsync<ClosedYearException>(() => context.SaveChangesAsync());

        await using var verify = c.Db.CreateContext();
        Assert.Equal(1, await verify.Assessments.CountAsync());
    }

    [Fact]
    [SuppressMessage("Usage", "VSTHRD103:Call async methods when in an async method", Justification = "Exercises the synchronous SaveChanges path, which uses the sync SchoolYearResolver of the interceptor.")]
    public async Task SaveChanges_WritesAnAssessmentAuditEntry_ForANewAssessmentInAnUnlockedYear()
    {
        var c = await SeedClosedAsync(unlocked: true);
        using var _ = c.Db;

        await using (var context = c.Db.CreateContext())
        {
            context.Assessments.Add(new Assessment { GradeBucketId = c.Seed.BucketId, Title = "Test two", Date = Day });
            context.SaveChanges();
        }

        await AssertAuditedAsync(c, nameof(Assessment));
    }

    [Fact]
    public async Task SaveChangesAsync_WritesAnAssessmentAuditEntry_ForANewAssessmentInAnUnlockedYear()
    {
        var c = await SeedClosedAsync(unlocked: true);
        using var _ = c.Db;

        await using (var context = c.Db.CreateContext())
        {
            context.Assessments.Add(new Assessment { GradeBucketId = c.Seed.BucketId, Title = "Test two", Date = Day });
            await context.SaveChangesAsync();
        }

        await AssertAuditedAsync(c, nameof(Assessment));
    }

    // AssessmentService

    [Fact]
    public async Task CreateAssessmentAsync_Throws_ClosedYearException_InALockedYear()
    {
        var c = await SeedClosedAsync(unlocked: false);
        using var _ = c.Db;

        await Assert.ThrowsAsync<ClosedYearException>(
            () => CreateAssessmentService(c).CreateAssessmentAsync(c.Seed.BucketId, "Test two", Day));

        await using var verify = c.Db.CreateContext();
        Assert.Equal(1, await verify.Assessments.CountAsync());
        await AssertNothingAuditedAsync(c);
    }

    [Fact]
    public async Task CreateAssessmentAsync_WritesAnAuditEntry_InAnUnlockedYear()
    {
        var c = await SeedClosedAsync(unlocked: true);
        using var _ = c.Db;

        await CreateAssessmentService(c).CreateAssessmentAsync(c.Seed.BucketId, "Test two", Day);

        await AssertAuditedAsync(c, nameof(Assessment));
        await using var verify = c.Db.CreateContext();
        Assert.Equal(2, await verify.Assessments.CountAsync());
    }

    [Fact]
    public async Task EditAssessmentAsync_Throws_ClosedYearException_InALockedYear()
    {
        var c = await SeedClosedAsync(unlocked: false);
        using var _ = c.Db;

        await Assert.ThrowsAsync<ClosedYearException>(
            () => CreateAssessmentService(c).EditAssessmentAsync(c.AssessmentId, "Renamed", new DateOnly(2025, 9, 22)));

        await using var verify = c.Db.CreateContext();
        var assessment = await verify.Assessments.SingleAsync();
        Assert.Equal("Test one", assessment.Title);
        Assert.Equal(Day, assessment.Date);
        Assert.Equal(Day, (await verify.Grades.SingleAsync()).RecordedOn);
        await AssertNothingAuditedAsync(c);
    }

    [Fact]
    public async Task EditAssessmentAsync_WritesAnAuditEntry_InAnUnlockedYear()
    {
        var c = await SeedClosedAsync(unlocked: true);
        using var _ = c.Db;

        await CreateAssessmentService(c).EditAssessmentAsync(c.AssessmentId, "Renamed", new DateOnly(2025, 9, 22));

        await AssertAuditedAsync(c, nameof(Assessment));
        await AssertAuditedAsync(c, nameof(Grade));
        await using var verify = c.Db.CreateContext();
        Assert.Equal("Renamed", (await verify.Assessments.SingleAsync()).Title);
        Assert.Equal(new DateOnly(2025, 9, 22), (await verify.Grades.SingleAsync()).RecordedOn);
    }

    [Fact]
    public async Task VoidAssessmentAsync_Throws_ClosedYearException_InALockedYear()
    {
        var c = await SeedClosedAsync(unlocked: false);
        using var _ = c.Db;

        await Assert.ThrowsAsync<ClosedYearException>(() => CreateAssessmentService(c).VoidAssessmentAsync(c.AssessmentId));

        await using var verify = c.Db.CreateContext();
        Assert.Null((await verify.Assessments.SingleAsync()).VoidedOn);
        await AssertNothingAuditedAsync(c);
    }

    [Fact]
    public async Task VoidAssessmentAsync_WritesAnAuditEntry_InAnUnlockedYear()
    {
        var c = await SeedClosedAsync(unlocked: true);
        using var _ = c.Db;

        await CreateAssessmentService(c).VoidAssessmentAsync(c.AssessmentId);

        await AssertAuditedAsync(c, nameof(Assessment));
        await using var verify = c.Db.CreateContext();
        Assert.NotNull((await verify.Assessments.SingleAsync()).VoidedOn);
    }

    [Fact]
    public async Task RestoreAssessmentAsync_Throws_ClosedYearException_InALockedYear()
    {
        var c = await SeedClosedAsync(unlocked: false);
        using var _ = c.Db;
        await using (var context = c.Db.CreateContext())
        {
            // Voided before the year was closed.
            await context.Database.ExecuteSqlRawAsync("UPDATE Assessments SET VoidedOn = '2025-10-01 00:00:00'");
        }

        await Assert.ThrowsAsync<ClosedYearException>(() => CreateAssessmentService(c).RestoreAssessmentAsync(c.AssessmentId));

        await using var verify = c.Db.CreateContext();
        Assert.NotNull((await verify.Assessments.SingleAsync()).VoidedOn);
        await AssertNothingAuditedAsync(c);
    }

    [Fact]
    public async Task RestoreAssessmentAsync_WritesAnAuditEntry_InAnUnlockedYear()
    {
        var c = await SeedClosedAsync(unlocked: true);
        using var _ = c.Db;
        await using (var context = c.Db.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync("UPDATE Assessments SET VoidedOn = '2025-10-01 00:00:00'");
        }

        await CreateAssessmentService(c).RestoreAssessmentAsync(c.AssessmentId);

        await AssertAuditedAsync(c, nameof(Assessment));
        await using var verify = c.Db.CreateContext();
        Assert.Null((await verify.Assessments.SingleAsync()).VoidedOn);
    }

    // GradeService

    [Fact]
    public async Task RecordAssessmentGradeAsync_Throws_ClosedYearException_InALockedYear()
    {
        var c = await SeedClosedAsync(unlocked: false);
        using var _ = c.Db;
        var other = c.OtherStudentId;

        await Assert.ThrowsAsync<ClosedYearException>(
            () => CreateGradeService(c).RecordAssessmentGradeAsync(c.AssessmentId, other, 3.0m));

        await using var verify = c.Db.CreateContext();
        Assert.Equal(1, await verify.Grades.CountAsync());
        await AssertNothingAuditedAsync(c);
    }

    [Fact]
    public async Task UpdateGradeAsync_Throws_ClosedYearException_InALockedYear()
    {
        var c = await SeedClosedAsync(unlocked: false);
        using var _ = c.Db;

        await Assert.ThrowsAsync<ClosedYearException>(
            () => CreateGradeService(c).UpdateGradeAsync(c.GradeId, 3.0m, Day));

        await using var verify = c.Db.CreateContext();
        Assert.Equal(2.0m, (await verify.Grades.SingleAsync()).Value);
        await AssertNothingAuditedAsync(c);
    }

    [Fact]
    public async Task UpdateGradeAsync_WritesAGradeAuditEntry_InAnUnlockedYear()
    {
        var c = await SeedClosedAsync(unlocked: true);
        using var _ = c.Db;

        await CreateGradeService(c).UpdateGradeAsync(c.GradeId, 3.0m, Day);

        await AssertAuditedAsync(c, nameof(Grade));
        await using var verify = c.Db.CreateContext();
        Assert.Equal(3.0m, (await verify.Grades.SingleAsync()).Value);
    }

    [Fact]
    public async Task RecordAssessmentGradeAsync_WritesAGradeAuditEntry_InAnUnlockedYear()
    {
        var c = await SeedClosedAsync(unlocked: true);
        using var _ = c.Db;

        await CreateGradeService(c).RecordAssessmentGradeAsync(c.AssessmentId, c.OtherStudentId, 3.0m);

        await AssertAuditedAsync(c, nameof(Grade));
        await using var verify = c.Db.CreateContext();
        Assert.Equal(2, await verify.Grades.CountAsync());
    }

    [Fact]
    public async Task VoidGradeAsync_Throws_ClosedYearException_InALockedYear()
    {
        var c = await SeedClosedAsync(unlocked: false);
        using var _ = c.Db;

        await Assert.ThrowsAsync<ClosedYearException>(() => CreateGradeService(c).VoidGradeAsync(c.GradeId));

        await using var verify = c.Db.CreateContext();
        Assert.Null((await verify.Grades.SingleAsync()).VoidedOn);
        await AssertNothingAuditedAsync(c);
    }

    [Fact]
    public async Task VoidGradeAsync_WritesAGradeAuditEntry_InAnUnlockedYear()
    {
        var c = await SeedClosedAsync(unlocked: true);
        using var _ = c.Db;

        await CreateGradeService(c).VoidGradeAsync(c.GradeId);

        await AssertAuditedAsync(c, nameof(Grade));
        await using var verify = c.Db.CreateContext();
        Assert.NotNull((await verify.Grades.SingleAsync()).VoidedOn);
    }

    [Fact]
    public async Task RestoreGradeAsync_Throws_ClosedYearException_InALockedYear()
    {
        var c = await SeedClosedAsync(unlocked: false);
        using var _ = c.Db;
        await using (var context = c.Db.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync("UPDATE Grades SET VoidedOn = '2025-10-01 00:00:00'");
        }

        await Assert.ThrowsAsync<ClosedYearException>(() => CreateGradeService(c).RestoreGradeAsync(c.GradeId));

        await using var verify = c.Db.CreateContext();
        Assert.NotNull((await verify.Grades.SingleAsync()).VoidedOn);
        await AssertNothingAuditedAsync(c);
    }

    [Fact]
    public async Task RestoreGradeAsync_WritesAGradeAuditEntry_InAnUnlockedYear()
    {
        var c = await SeedClosedAsync(unlocked: true);
        using var _ = c.Db;
        await using (var context = c.Db.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync("UPDATE Grades SET VoidedOn = '2025-10-01 00:00:00'");
        }

        await CreateGradeService(c).RestoreGradeAsync(c.GradeId);

        await AssertAuditedAsync(c, nameof(Grade));
        await using var verify = c.Db.CreateContext();
        Assert.Null((await verify.Grades.SingleAsync()).VoidedOn);
    }
}
