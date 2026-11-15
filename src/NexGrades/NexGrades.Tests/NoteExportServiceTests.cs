using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Export;

namespace NexGrades.Tests;

public class NoteExportServiceTests
{
    private static readonly DateTime ExportTime = new(2026, 3, 1, 10, 30, 0);

    private sealed record Fixture(int StudentId, int YearId, int ClassId);

    private static async Task<Fixture> SeedAsync(SqliteTestDatabase db)
    {
        await using var context = db.CreateContext();
        var year = new SchoolYear
        {
            Label = "2025/26",
            StartDate = new DateTime(2025, 8, 1),
            EndDate = new DateTime(2026, 7, 31),
            Status = SchoolYearStatus.Active,
        };
        var group = new ClassGroup { CohortLabel = "Test cohort", FoundedInSchoolYear = year };
        var classInstance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A" };
        var student = new Student { FirstName = "Test", LastName = "Alpha" };
        context.AddRange(year, group, classInstance, student);
        await context.SaveChangesAsync();
        return new Fixture(student.Id, year.Id, classInstance.Id);
    }

    private static async Task AddNoteAsync(
        SqliteTestDatabase db,
        Fixture fixture,
        string text,
        DateTime createdOn,
        DateTime? archivedOn = null,
        int? classId = null,
        int? yearId = null,
        int? studentId = null,
        string? category = null)
    {
        await using var context = db.CreateContext();
        context.Add(new Note
        {
            StudentId = studentId ?? fixture.StudentId,
            SchoolYearId = yearId ?? fixture.YearId,
            ClassInstanceId = classId ?? fixture.ClassId,
            Text = text,
            Category = category,
            CreatedOn = createdOn,
            ArchivedOn = archivedOn,
        });
        await context.SaveChangesAsync();
    }

    private static NoteExportService CreateService(SqliteTestDatabase db) =>
        new(db.CreateContextFactory(), new FixedClock(ExportTime));

    private static string[] Texts(NoteExportTable table) => [.. table.Rows.Select(r => r.Text)];

    [Fact]
    public async Task BuildAsync_ReturnsNotesInRange_IncludingBoundaryDays()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        await AddNoteAsync(db, fixture, "first day", new DateTime(2025, 10, 1, 0, 0, 0));
        await AddNoteAsync(db, fixture, "last day late", new DateTime(2025, 10, 31, 23, 59, 59));

        var table = await CreateService(db).BuildAsync(fixture.StudentId, new DateOnly(2025, 10, 1), new DateOnly(2025, 10, 31), false);

        Assert.Equal(["first day", "last day late"], Texts(table));
        Assert.Equal(2, table.NoteCount);
    }

    [Fact]
    public async Task BuildAsync_ExcludesNotesOutsideRange()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        await AddNoteAsync(db, fixture, "before", new DateTime(2025, 9, 30, 23, 59, 59));
        await AddNoteAsync(db, fixture, "inside", new DateTime(2025, 10, 15, 8, 0, 0));
        await AddNoteAsync(db, fixture, "after", new DateTime(2025, 11, 1, 0, 0, 0));

        var table = await CreateService(db).BuildAsync(fixture.StudentId, new DateOnly(2025, 10, 1), new DateOnly(2025, 10, 31), false);

        Assert.Equal(["inside"], Texts(table));
    }

    [Fact]
    public async Task BuildAsync_ReturnsSingleDayRange_WhenFromEqualsTo()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        await AddNoteAsync(db, fixture, "same day", new DateTime(2025, 10, 5, 15, 0, 0));
        await AddNoteAsync(db, fixture, "next day", new DateTime(2025, 10, 6, 0, 0, 0));

        var table = await CreateService(db).BuildAsync(fixture.StudentId, new DateOnly(2025, 10, 5), new DateOnly(2025, 10, 5), false);

        Assert.Equal(["same day"], Texts(table));
    }

    [Fact]
    public async Task BuildAsync_ExcludesArchivedNotes_ByDefault()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        await AddNoteAsync(db, fixture, "active", new DateTime(2025, 10, 1));
        await AddNoteAsync(db, fixture, "archived", new DateTime(2025, 10, 2), archivedOn: new DateTime(2025, 10, 3));

        var table = await CreateService(db).BuildAsync(fixture.StudentId, new DateOnly(2025, 10, 1), new DateOnly(2025, 10, 31), false);

        Assert.Equal(["active"], Texts(table));
        Assert.False(table.IncludeArchived);
    }

    [Fact]
    public async Task BuildAsync_IncludesArchivedNotesWithArchivedOn_WhenRequested()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        var archivedOn = new DateTime(2025, 10, 3, 9, 0, 0);
        await AddNoteAsync(db, fixture, "active", new DateTime(2025, 10, 1));
        await AddNoteAsync(db, fixture, "archived", new DateTime(2025, 10, 2), archivedOn: archivedOn);

        var table = await CreateService(db).BuildAsync(fixture.StudentId, new DateOnly(2025, 10, 1), new DateOnly(2025, 10, 31), true);

        Assert.Equal(["active", "archived"], Texts(table));
        Assert.True(table.IncludeArchived);
        Assert.Null(table.Rows[0].ArchivedOn);
        Assert.Equal(archivedOn, table.Rows[1].ArchivedOn);
    }

    [Fact]
    public async Task BuildAsync_OrdersNotesOldestFirst_NotByInsertion()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        await AddNoteAsync(db, fixture, "newest", new DateTime(2025, 12, 1));
        await AddNoteAsync(db, fixture, "oldest", new DateTime(2025, 10, 1));
        await AddNoteAsync(db, fixture, "middle", new DateTime(2025, 11, 1));

        var table = await CreateService(db).BuildAsync(fixture.StudentId, new DateOnly(2025, 8, 1), new DateOnly(2026, 7, 31), false);

        Assert.Equal(["oldest", "middle", "newest"], Texts(table));
    }

    [Fact]
    public async Task BuildAsync_OrdersSameTimestampNotesById()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        var at = new DateTime(2025, 10, 1, 8, 0, 0);
        await AddNoteAsync(db, fixture, "one", at);
        await AddNoteAsync(db, fixture, "two", at);
        await AddNoteAsync(db, fixture, "three", at);

        var table = await CreateService(db).BuildAsync(fixture.StudentId, new DateOnly(2025, 10, 1), new DateOnly(2025, 10, 1), false);

        Assert.Equal(["one", "two", "three"], Texts(table));
    }

    [Fact]
    public async Task BuildAsync_ReturnsClassNamePerNote_ForStudentWhoChangedClass()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        int otherClassId;
        await using (var context = db.CreateContext())
        {
            var group = new ClassGroup { CohortLabel = "Other cohort", FoundedInSchoolYearId = fixture.YearId };
            var other = new ClassInstance { ClassGroup = group, SchoolYearId = fixture.YearId, Name = "5B" };
            context.AddRange(group, other);
            await context.SaveChangesAsync();
            otherClassId = other.Id;
        }

        await AddNoteAsync(db, fixture, "in 5A", new DateTime(2025, 10, 1));
        await AddNoteAsync(db, fixture, "in 5B", new DateTime(2025, 11, 1), classId: otherClassId);

        var table = await CreateService(db).BuildAsync(fixture.StudentId, new DateOnly(2025, 8, 1), new DateOnly(2026, 7, 31), false);

        Assert.Equal(["5A", "5B"], [.. table.Rows.Select(r => r.ClassName!)]);
    }

    [Fact]
    public async Task BuildAsync_ReturnsNoRowsAndNoteCountZero_WhenNothingMatches()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        await AddNoteAsync(db, fixture, "elsewhere", new DateTime(2025, 12, 1));

        var table = await CreateService(db).BuildAsync(fixture.StudentId, new DateOnly(2025, 10, 1), new DateOnly(2025, 10, 31), true);

        Assert.Empty(table.Rows);
        Assert.Equal(0, table.NoteCount);
        Assert.Equal("Test", table.FirstName);
        Assert.Equal("Alpha", table.LastName);
    }

    [Fact]
    public async Task BuildAsync_ExcludesNotesOfOtherStudents()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        int otherStudentId;
        await using (var context = db.CreateContext())
        {
            var other = new Student { FirstName = "Test", LastName = "Beta" };
            context.Add(other);
            await context.SaveChangesAsync();
            otherStudentId = other.Id;
        }

        await AddNoteAsync(db, fixture, "mine", new DateTime(2025, 10, 1));
        await AddNoteAsync(db, fixture, "theirs", new DateTime(2025, 10, 1), studentId: otherStudentId);

        var table = await CreateService(db).BuildAsync(fixture.StudentId, new DateOnly(2025, 10, 1), new DateOnly(2025, 10, 31), false);

        Assert.Equal(["mine"], Texts(table));
    }

    [Fact]
    public async Task BuildAsync_SpansTwoSchoolYears_IncludingClosedYear()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        int closedYearId;
        int closedClassId;
        await using (var context = db.CreateContext())
        {
            var closed = new SchoolYear
            {
                Label = "2024/25",
                StartDate = new DateTime(2024, 8, 1),
                EndDate = new DateTime(2025, 7, 31),
                Status = SchoolYearStatus.Closed,
            };
            var group = new ClassGroup { CohortLabel = "Old cohort", FoundedInSchoolYear = closed };
            var oldClass = new ClassInstance { ClassGroup = group, SchoolYear = closed, Name = "4A" };
            context.AddRange(closed, group, oldClass);
            await context.SaveChangesAsync();
            closedYearId = closed.Id;
            closedClassId = oldClass.Id;
        }

        await AddNoteAsync(db, fixture, "old", new DateTime(2025, 6, 1), yearId: closedYearId, classId: closedClassId);
        await AddNoteAsync(db, fixture, "new", new DateTime(2025, 10, 1));

        var table = await CreateService(db).BuildAsync(fixture.StudentId, new DateOnly(2025, 5, 1), new DateOnly(2025, 12, 31), false);

        Assert.Equal(["old", "new"], Texts(table));
        Assert.Equal(["2024/25", "2025/26"], [.. table.Rows.Select(r => r.SchoolYearLabel)]);
        Assert.Equal(["4A", "5A"], [.. table.Rows.Select(r => r.ClassName!)]);
        await using var verify = db.CreateContext();
        Assert.Empty(verify.AuditEntries);
        Assert.Equal(2, verify.Notes.Count());
    }

    [Fact]
    public async Task BuildAsync_ReturnsNullClassName_ForNoteWithoutClass()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        await using (var context = db.CreateContext())
        {
            context.Add(new Note
            {
                StudentId = fixture.StudentId,
                SchoolYearId = fixture.YearId,
                ClassInstanceId = null,
                Text = "no class",
                CreatedOn = new DateTime(2025, 10, 1),
            });
            await context.SaveChangesAsync();
        }

        var table = await CreateService(db).BuildAsync(fixture.StudentId, new DateOnly(2025, 10, 1), new DateOnly(2025, 10, 31), false);

        Assert.Null(Assert.Single(table.Rows).ClassName);
    }

    [Fact]
    public async Task BuildAsync_Throws_WhenFromIsAfterTo()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        await AddNoteAsync(db, fixture, "note", new DateTime(2025, 10, 1));

        await Assert.ThrowsAsync<InvariantViolationException>(() =>
            CreateService(db).BuildAsync(fixture.StudentId, new DateOnly(2025, 11, 1), new DateOnly(2025, 10, 1), false));

        await using var context = db.CreateContext();
        Assert.Equal(1, context.Notes.Count());
        Assert.Empty(context.AuditEntries);
    }

    [Fact]
    public async Task BuildAsync_Throws_ForUnknownStudent()
    {
        using var db = new SqliteTestDatabase();
        await SeedAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(() =>
            CreateService(db).BuildAsync(9999, new DateOnly(2025, 10, 1), new DateOnly(2025, 10, 31), false));

        await using var context = db.CreateContext();
        Assert.Empty(context.AuditEntries);
    }

    [Fact]
    public async Task BuildAsync_SetsExportedOnFromClock()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);

        var table = await CreateService(db).BuildAsync(fixture.StudentId, new DateOnly(2025, 10, 1), new DateOnly(2025, 10, 31), false);

        Assert.Equal(ExportTime, table.ExportedOn);
        Assert.Equal(new DateOnly(2025, 10, 1), table.From);
        Assert.Equal(new DateOnly(2025, 10, 31), table.To);
    }

    [Fact]
    public async Task BuildAsync_MapsNoteFields()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        var edited = new DateTime(2025, 10, 2, 12, 0, 0);
        await using (var context = db.CreateContext())
        {
            context.Add(new Note
            {
                StudentId = fixture.StudentId,
                SchoolYearId = fixture.YearId,
                ClassInstanceId = fixture.ClassId,
                Text = "body",
                Category = "Behaviour",
                CreatedOn = new DateTime(2025, 10, 1, 8, 0, 0),
                LastEditedOn = edited,
            });
            await context.SaveChangesAsync();
        }

        var row = Assert.Single((await CreateService(db).BuildAsync(
            fixture.StudentId, new DateOnly(2025, 10, 1), new DateOnly(2025, 10, 1), false)).Rows);

        Assert.Equal("Behaviour", row.Category);
        Assert.Equal(edited, row.LastEditedOn);
        Assert.Equal("2025/26", row.SchoolYearLabel);
        Assert.Null(row.ArchivedOn);
    }
}
