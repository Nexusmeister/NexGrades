using Microsoft.EntityFrameworkCore;
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
}
