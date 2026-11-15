using Microsoft.EntityFrameworkCore;
using NexGrades.Data.Entities;
using NexGrades.Domain.Auditing;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class SubjectServiceTests
{
    private static async Task<int> SubjectCountAsync(SqliteTestDatabase db)
    {
        await using var context = db.CreateContext();
        return await context.Subjects.CountAsync();
    }

    [Fact]
    public async Task CreateSubjectAsync_CreatesActiveSubject_WithTrimmedValuesAndNoTemplates()
    {
        using var db = new SqliteTestDatabase();
        var service = new SubjectService(db.CreateContextFactory());

        var created = await service.CreateSubjectAsync("  Test Subject ", " TS ");

        await using var verify = db.CreateContext();
        var subject = await verify.Subjects.AsNoTracking().Include(s => s.BucketTemplates).SingleAsync();
        Assert.Equal(created.Id, subject.Id);
        Assert.Equal("Test Subject", subject.Name);
        Assert.Equal("TS", subject.ShortCode);
        Assert.True(subject.IsActive);
        Assert.Empty(subject.BucketTemplates);
    }

    [Theory]
    [InlineData("", "TS")]
    [InlineData("   ", "TS")]
    [InlineData("Test Subject", "")]
    [InlineData("Test Subject", "  ")]
    public async Task CreateSubjectAsync_Throws_ForEmptyNameOrShortCode(string name, string shortCode)
    {
        using var db = new SqliteTestDatabase();
        var service = new SubjectService(db.CreateContextFactory());

        await Assert.ThrowsAsync<InvariantViolationException>(() => service.CreateSubjectAsync(name, shortCode));

        Assert.Equal(0, await SubjectCountAsync(db));
    }

    [Fact]
    public async Task CreateSubjectAsync_Throws_ForTooLongName()
    {
        using var db = new SqliteTestDatabase();
        var service = new SubjectService(db.CreateContextFactory());

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => service.CreateSubjectAsync(new string('a', SubjectService.MaxNameLength + 1), "TS"));

        Assert.Equal(0, await SubjectCountAsync(db));
    }

    [Fact]
    public async Task CreateSubjectAsync_Throws_ForTooLongShortCode()
    {
        using var db = new SqliteTestDatabase();
        var service = new SubjectService(db.CreateContextFactory());

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => service.CreateSubjectAsync("Test Subject", new string('a', SubjectService.MaxShortCodeLength + 1)));

        Assert.Equal(0, await SubjectCountAsync(db));
    }

    [Fact]
    public async Task CreateSubjectAsync_Succeeds_AtMaxLengths()
    {
        using var db = new SqliteTestDatabase();
        var service = new SubjectService(db.CreateContextFactory());
        var name = new string('a', SubjectService.MaxNameLength);
        var shortCode = new string('b', SubjectService.MaxShortCodeLength);

        await service.CreateSubjectAsync(name, shortCode);

        await using var verify = db.CreateContext();
        var subject = await verify.Subjects.AsNoTracking().SingleAsync();
        Assert.Equal(name, subject.Name);
        Assert.Equal(shortCode, subject.ShortCode);
    }

    private static async Task<int> SeedSubjectAsync(SqliteTestDatabase db, string name = "Testfach", string shortCode = "TF")
    {
        await using var context = db.CreateContext();
        var subject = new Subject { Name = name, ShortCode = shortCode };
        context.Subjects.Add(subject);
        await context.SaveChangesAsync();
        return subject.Id;
    }

    [Fact]
    public async Task UpdateSubjectAsync_PersistsTrimmedNameAndShortCode_ForExistingSubject()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedSubjectAsync(db);
        var service = new SubjectService(db.CreateContextFactory());

        await service.UpdateSubjectAsync(id, "  Neues Fach ", " NF ");

        await using var verify = db.CreateContext();
        var subject = await verify.Subjects.AsNoTracking().SingleAsync();
        Assert.Equal(id, subject.Id);
        Assert.Equal("Neues Fach", subject.Name);
        Assert.Equal("NF", subject.ShortCode);
    }

    [Fact]
    public async Task UpdateSubjectAsync_LeavesBucketTemplatesUnchanged_ForSubjectWithTemplates()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedSubjectAsync(db);
        await using (var seed = db.CreateContext())
        {
            seed.BucketTemplates.Add(new BucketTemplate { SubjectId = id, Name = "Written", Weight = 60.5m, SortOrder = 1 });
            await seed.SaveChangesAsync();
        }

        var service = new SubjectService(db.CreateContextFactory());

        await service.UpdateSubjectAsync(id, "Neues Fach", "NF");

        await using var verify = db.CreateContext();
        var template = await verify.BucketTemplates.AsNoTracking().SingleAsync();
        Assert.Equal(id, template.SubjectId);
        Assert.Equal("Written", template.Name);
        Assert.Equal(60.5m, template.Weight);
        Assert.Equal(1, template.SortOrder);
    }

    [Fact]
    public async Task UpdateSubjectAsync_Succeeds_ForSubjectUsedInClosedYearWithoutAuditEntries()
    {
        var clock = new FixedClock(new DateTime(2026, 8, 15, 10, 0, 0));
        using var db = new SqliteTestDatabase(new ClosedYearAuditInterceptor(clock));
        var seed = await GradeSheetSeed.CreateAsync(db);
        await seed.SetYearStatusAsync(SchoolYearStatus.Closed);
        int subjectId;
        await using (var read = db.CreateContext())
        {
            subjectId = await read.Subjects.Select(s => s.Id).SingleAsync();
        }

        var service = new SubjectService(db.CreateContextFactory());

        await service.UpdateSubjectAsync(subjectId, "Neues Fach", "NF");

        await using var verify = db.CreateContext();
        var subject = await verify.Subjects.AsNoTracking().SingleAsync();
        Assert.Equal("Neues Fach", subject.Name);
        Assert.Equal("NF", subject.ShortCode);
        Assert.Empty(await verify.AuditEntries.ToListAsync());
    }

    [Fact]
    public async Task UpdateSubjectAsync_Throws_ForUnknownSubject()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedSubjectAsync(db);
        var service = new SubjectService(db.CreateContextFactory());

        await Assert.ThrowsAsync<InvariantViolationException>(() => service.UpdateSubjectAsync(id + 1, "Neues Fach", "NF"));

        Assert.Equal(1, await SubjectCountAsync(db));
        await using var verify = db.CreateContext();
        var subject = await verify.Subjects.AsNoTracking().SingleAsync();
        Assert.Equal("Testfach", subject.Name);
    }

    [Theory]
    [InlineData("", "NF")]
    [InlineData("   ", "NF")]
    [InlineData("Neues Fach", "")]
    [InlineData("Neues Fach", "  ")]
    public async Task UpdateSubjectAsync_Throws_ForEmptyNameOrShortCode(string name, string shortCode)
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedSubjectAsync(db);
        var service = new SubjectService(db.CreateContextFactory());

        await Assert.ThrowsAsync<InvariantViolationException>(() => service.UpdateSubjectAsync(id, name, shortCode));

        await AssertUnchangedAsync(db);
    }

    [Fact]
    public async Task UpdateSubjectAsync_Throws_ForTooLongName()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedSubjectAsync(db);
        var service = new SubjectService(db.CreateContextFactory());

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => service.UpdateSubjectAsync(id, new string('a', SubjectService.MaxNameLength + 1), "NF"));

        await AssertUnchangedAsync(db);
    }

    [Fact]
    public async Task UpdateSubjectAsync_Throws_ForTooLongShortCode()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedSubjectAsync(db);
        var service = new SubjectService(db.CreateContextFactory());

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => service.UpdateSubjectAsync(id, "Neues Fach", new string('b', SubjectService.MaxShortCodeLength + 1)));

        await AssertUnchangedAsync(db);
    }

    [Fact]
    public async Task UpdateSubjectAsync_Succeeds_AtMaxLengths()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedSubjectAsync(db);
        var service = new SubjectService(db.CreateContextFactory());
        var name = new string('a', SubjectService.MaxNameLength);
        var shortCode = new string('b', SubjectService.MaxShortCodeLength);

        await service.UpdateSubjectAsync(id, name, shortCode);

        await using var verify = db.CreateContext();
        var subject = await verify.Subjects.AsNoTracking().SingleAsync();
        Assert.Equal(name, subject.Name);
        Assert.Equal(shortCode, subject.ShortCode);
    }

    [Fact]
    public async Task FindSubjectAsync_ReturnsSubject_ForExistingId()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedSubjectAsync(db, "Testfach", "TF");
        var service = new SubjectService(db.CreateContextFactory());

        var subject = await service.FindSubjectAsync(id);

        Assert.NotNull(subject);
        Assert.Equal(id, subject.Id);
        Assert.Equal("Testfach", subject.Name);
        Assert.Equal("TF", subject.ShortCode);
    }

    [Fact]
    public async Task FindSubjectAsync_ReturnsNull_ForUnknownId()
    {
        using var db = new SqliteTestDatabase();
        var id = await SeedSubjectAsync(db);
        var service = new SubjectService(db.CreateContextFactory());

        Assert.Null(await service.FindSubjectAsync(id + 1));
    }

    private static async Task AssertUnchangedAsync(SqliteTestDatabase db)
    {
        await using var verify = db.CreateContext();
        var subject = await verify.Subjects.AsNoTracking().SingleAsync();
        Assert.Equal("Testfach", subject.Name);
        Assert.Equal("TF", subject.ShortCode);
    }
}
