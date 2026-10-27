using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Auditing;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class NoteServiceTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0);

    private sealed class Env : IDisposable
    {
        public SqliteTestDatabase Db { get; }
        public FixedClock Clock { get; }
        public UnlockReasonStore Reasons { get; } = new();
        public NoteService Service { get; }

        public Env()
        {
            Clock = new FixedClock(Now);
            Db = new SqliteTestDatabase(new ClosedYearAuditInterceptor(Clock, Reasons));
            Service = new NoteService(new TestDbContextFactory(Db), Clock);
        }

        /// <summary>A context without the interceptor, so seeding is allowed in any year state.</summary>
        public AppDbContext SeedContext() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Db.DbPath}").Options);

        public void Dispose() => Db.Dispose();
    }

    private sealed record Seed(int StudentId, int YearId, int ClassInstanceId);

    private static async Task<Seed> SeedAsync(
        Env env,
        SchoolYearStatus status = SchoolYearStatus.Active,
        DateTime? unlockedUntil = null,
        bool enrolled = true,
        DateTime? leftOn = null,
        string label = "2025/26",
        int startYear = 2025,
        string firstName = "Anna")
    {
        await using var context = env.SeedContext();
        var year = new SchoolYear
        {
            Label = label,
            StartDate = new DateTime(startYear, 8, 1),
            EndDate = new DateTime(startYear + 1, 7, 31),
            Status = status,
            UnlockedUntil = unlockedUntil,
            ClosedOn = status == SchoolYearStatus.Closed ? new DateTime(startYear + 1, 7, 31) : null,
        };
        var group = new ClassGroup { CohortLabel = $"Kohorte {startYear}", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A" };
        var student = new Student { FirstName = firstName, LastName = "Schmidt" };
        context.AddRange(year, group, instance, student);
        if (enrolled)
        {
            context.Enrollments.Add(new Enrollment
            {
                Student = student,
                ClassInstance = instance,
                JoinedOn = new DateTime(startYear, 8, 1),
                LeftOn = leftOn,
            });
        }

        await context.SaveChangesAsync();
        return new Seed(student.Id, year.Id, instance.Id);
    }

    /// <summary>Adds a student with an enrollment in an already-seeded year.</summary>
    private static async Task<int> SeedStudentInYearAsync(Env env, Seed seed, string firstName)
    {
        await using var context = env.SeedContext();
        var student = new Student { FirstName = firstName, LastName = "Meier" };
        context.Students.Add(student);
        context.Enrollments.Add(new Enrollment
        {
            Student = student,
            ClassInstanceId = seed.ClassInstanceId,
            JoinedOn = new DateTime(2025, 8, 1),
        });
        await context.SaveChangesAsync();
        return student.Id;
    }

    private static async Task<int> SeedNoteAsync(
        Env env,
        Seed seed,
        string text = "Note",
        DateTime? createdOn = null,
        DateTime? archivedOn = null,
        int? studentId = null,
        string? category = null)
    {
        await using var context = env.SeedContext();
        var note = new Note
        {
            StudentId = studentId ?? seed.StudentId,
            SchoolYearId = seed.YearId,
            ClassInstanceId = seed.ClassInstanceId,
            Text = text,
            Category = category,
            CreatedOn = createdOn ?? Now,
            ArchivedOn = archivedOn,
        };
        context.Notes.Add(note);
        await context.SaveChangesAsync();
        return note.Id;
    }

    private static async Task<Note> LoadNoteAsync(Env env, int noteId)
    {
        await using var context = env.Db.CreateContext();
        return await context.Notes.AsNoTracking().SingleAsync(n => n.Id == noteId);
    }

    private static async Task<int> NoteCountAsync(Env env)
    {
        await using var context = env.Db.CreateContext();
        return await context.Notes.CountAsync();
    }

    private static async Task<List<AuditEntry>> NoteAuditEntriesAsync(Env env)
    {
        await using var context = env.Db.CreateContext();
        return await context.AuditEntries.AsNoTracking().Where(a => a.EntityType == nameof(Note)).ToListAsync();
    }

    private static readonly DateTime FutureUnlock = Now.AddDays(1);

    // ---------- AddNoteAsync ----------

    [Fact]
    public async Task AddNoteAsync_Succeeds_ForEnrolledStudent()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);

        var dto = await env.Service.AddNoteAsync(seed.StudentId, seed.YearId, "Talked to parents");

        Assert.Equal(Now, dto.CreatedOn);
        Assert.Equal(seed.ClassInstanceId, dto.ClassInstanceId);
        Assert.Equal("5A", dto.ClassInstanceName);
        Assert.Equal("2025/26", dto.SchoolYearLabel);
        Assert.Null(dto.LastEditedOn);
        Assert.Null(dto.ArchivedOn);

        var stored = await LoadNoteAsync(env, dto.Id);
        Assert.Equal(seed.StudentId, stored.StudentId);
        Assert.Equal(seed.YearId, stored.SchoolYearId);
        Assert.Equal(seed.ClassInstanceId, stored.ClassInstanceId);
        Assert.Equal(Now, stored.CreatedOn);
    }

    [Fact]
    public async Task AddNoteAsync_TrimsText_AndNormalizesEmptyCategoryToNull()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);

        var withCategory = await env.Service.AddNoteAsync(seed.StudentId, seed.YearId, "  hello \n", "  Parents  ");
        var emptyCategory = await env.Service.AddNoteAsync(seed.StudentId, seed.YearId, "x", "   ");

        Assert.Equal("hello", withCategory.Text);
        Assert.Equal("Parents", withCategory.Category);
        Assert.Null(emptyCategory.Category);
        Assert.Null((await LoadNoteAsync(env, emptyCategory.Id)).Category);
    }

    [Fact]
    public async Task AddNoteAsync_Succeeds_ForStudentWhoHasLeftTheClass()
    {
        using var env = new Env();
        var seed = await SeedAsync(env, leftOn: new DateTime(2025, 12, 31));

        var dto = await env.Service.AddNoteAsync(seed.StudentId, seed.YearId, "History");

        Assert.Equal(seed.ClassInstanceId, dto.ClassInstanceId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n")]
    public async Task AddNoteAsync_Throws_ForEmptyOrWhitespaceText(string text)
    {
        using var env = new Env();
        var seed = await SeedAsync(env);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => env.Service.AddNoteAsync(seed.StudentId, seed.YearId, text));

        Assert.Equal(0, await NoteCountAsync(env));
    }

    [Fact]
    public async Task AddNoteAsync_Succeeds_ForTextAtMaxLength()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);
        var text = new string('a', NoteService.MaxTextLength);

        var dto = await env.Service.AddNoteAsync(seed.StudentId, seed.YearId, text);

        Assert.Equal(NoteService.MaxTextLength, (await LoadNoteAsync(env, dto.Id)).Text.Length);
    }

    [Fact]
    public async Task AddNoteAsync_Throws_ForTextOverMaxLength()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);
        var text = new string('a', NoteService.MaxTextLength + 1);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => env.Service.AddNoteAsync(seed.StudentId, seed.YearId, text));

        Assert.Equal(0, await NoteCountAsync(env));
    }

    [Fact]
    public async Task AddNoteAsync_Throws_ForCategoryOverMaxLength()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);
        var category = new string('c', NoteService.MaxCategoryLength + 1);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => env.Service.AddNoteAsync(seed.StudentId, seed.YearId, "text", category));

        Assert.Equal(0, await NoteCountAsync(env));
    }

    [Fact]
    public async Task AddNoteAsync_Throws_ForUnknownStudent()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => env.Service.AddNoteAsync(9999, seed.YearId, "text"));

        Assert.Equal(0, await NoteCountAsync(env));
    }

    [Fact]
    public async Task AddNoteAsync_Throws_ForUnknownSchoolYear()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => env.Service.AddNoteAsync(seed.StudentId, 9999, "text"));

        Assert.Equal(0, await NoteCountAsync(env));
    }

    [Fact]
    public async Task AddNoteAsync_Throws_WhenStudentHasNoEnrollmentInSchoolYear()
    {
        using var env = new Env();
        var seed = await SeedAsync(env, enrolled: false);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => env.Service.AddNoteAsync(seed.StudentId, seed.YearId, "text"));

        Assert.Equal(0, await NoteCountAsync(env));
    }

    [Fact]
    public async Task AddNoteAsync_Throws_ClosedYearException_ForLockedClosedYear()
    {
        using var env = new Env();
        var seed = await SeedAsync(env, SchoolYearStatus.Closed);

        await Assert.ThrowsAsync<ClosedYearException>(
            () => env.Service.AddNoteAsync(seed.StudentId, seed.YearId, "text"));

        Assert.Equal(0, await NoteCountAsync(env));
        Assert.Empty(await NoteAuditEntriesAsync(env));
    }

    [Fact]
    public async Task AddNoteAsync_Succeeds_AndWritesAuditEntry_ForUnlockedClosedYear()
    {
        using var env = new Env();
        var seed = await SeedAsync(env, SchoolYearStatus.Closed, FutureUnlock);
        env.Reasons.SetReason(seed.YearId, "Late note");

        var dto = await env.Service.AddNoteAsync(seed.StudentId, seed.YearId, "text");

        Assert.Equal(1, await NoteCountAsync(env));
        var entries = await NoteAuditEntriesAsync(env);
        Assert.NotEmpty(entries);
        Assert.All(entries, a => Assert.Equal("Late note", a.Reason));
        Assert.True(dto.Id > 0);
    }

    // ---------- EditNoteAsync ----------

    [Fact]
    public async Task EditNoteAsync_UpdatesTextAndSetsLastEditedOn()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);
        var noteId = await SeedNoteAsync(env, seed, "old", createdOn: Now.AddDays(-5));
        var editTime = Now.AddHours(3);
        env.Clock.Now = editTime;

        var dto = await env.Service.EditNoteAsync(noteId, "  new  ", "Cat");

        Assert.Equal("new", dto.Text);
        Assert.Equal("Cat", dto.Category);
        Assert.Equal(editTime, dto.LastEditedOn);
        var stored = await LoadNoteAsync(env, noteId);
        Assert.Equal("new", stored.Text);
        Assert.Equal("Cat", stored.Category);
        Assert.Equal(editTime, stored.LastEditedOn);
        Assert.Equal(Now.AddDays(-5), stored.CreatedOn);
    }

    [Fact]
    public async Task EditNoteAsync_DoesNotSave_WhenNothingChanged()
    {
        using var env = new Env();
        var seed = await SeedAsync(env, SchoolYearStatus.Closed, FutureUnlock);
        var noteId = await SeedNoteAsync(env, seed, "same", category: "Cat");
        env.Reasons.SetReason(seed.YearId, "No-op");

        var dto = await env.Service.EditNoteAsync(noteId, " same ", "Cat");

        Assert.Null(dto.LastEditedOn);
        Assert.Null((await LoadNoteAsync(env, noteId)).LastEditedOn);
        Assert.Empty(await NoteAuditEntriesAsync(env));
    }

    [Fact]
    public async Task EditNoteAsync_Throws_ForUnknownNote()
    {
        using var env = new Env();
        await SeedAsync(env);

        await Assert.ThrowsAsync<InvariantViolationException>(() => env.Service.EditNoteAsync(9999, "text"));
    }

    [Fact]
    public async Task EditNoteAsync_Throws_ForArchivedNote()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);
        var noteId = await SeedNoteAsync(env, seed, "old", archivedOn: Now.AddDays(-1));

        await Assert.ThrowsAsync<InvariantViolationException>(() => env.Service.EditNoteAsync(noteId, "new"));

        var stored = await LoadNoteAsync(env, noteId);
        Assert.Equal("old", stored.Text);
        Assert.Null(stored.LastEditedOn);
    }

    [Fact]
    public async Task EditNoteAsync_Throws_ForEmptyText()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);
        var noteId = await SeedNoteAsync(env, seed, "old");

        await Assert.ThrowsAsync<InvariantViolationException>(() => env.Service.EditNoteAsync(noteId, "  "));

        Assert.Equal("old", (await LoadNoteAsync(env, noteId)).Text);
    }

    [Fact]
    public async Task EditNoteAsync_Throws_ClosedYearException_ForLockedClosedYear()
    {
        using var env = new Env();
        var seed = await SeedAsync(env, SchoolYearStatus.Closed);
        var noteId = await SeedNoteAsync(env, seed, "old");

        await Assert.ThrowsAsync<ClosedYearException>(() => env.Service.EditNoteAsync(noteId, "new"));

        var stored = await LoadNoteAsync(env, noteId);
        Assert.Equal("old", stored.Text);
        Assert.Null(stored.LastEditedOn);
        Assert.Empty(await NoteAuditEntriesAsync(env));
    }

    [Fact]
    public async Task EditNoteAsync_WritesAuditEntries_ForUnlockedClosedYear()
    {
        using var env = new Env();
        var seed = await SeedAsync(env, SchoolYearStatus.Closed, FutureUnlock);
        var noteId = await SeedNoteAsync(env, seed, "old");
        env.Reasons.SetReason(seed.YearId, "Fix typo");

        await env.Service.EditNoteAsync(noteId, "new");

        Assert.Equal("new", (await LoadNoteAsync(env, noteId)).Text);
        var entries = await NoteAuditEntriesAsync(env);
        Assert.NotEmpty(entries);
        Assert.All(entries, a => Assert.Equal("Fix typo", a.Reason));
    }

    // ---------- Archive / restore ----------

    [Fact]
    public async Task ArchiveNoteAsync_SetsArchivedOn()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);
        var noteId = await SeedNoteAsync(env, seed);

        await env.Service.ArchiveNoteAsync(noteId);

        Assert.Equal(Now, (await LoadNoteAsync(env, noteId)).ArchivedOn);
    }

    [Fact]
    public async Task ArchiveNoteAsync_Throws_WhenAlreadyArchived()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);
        var archivedOn = Now.AddDays(-2);
        var noteId = await SeedNoteAsync(env, seed, archivedOn: archivedOn);

        await Assert.ThrowsAsync<InvariantViolationException>(() => env.Service.ArchiveNoteAsync(noteId));

        Assert.Equal(archivedOn, (await LoadNoteAsync(env, noteId)).ArchivedOn);
    }

    [Fact]
    public async Task ArchiveNoteAsync_Throws_ForUnknownNote()
    {
        using var env = new Env();
        await SeedAsync(env);

        await Assert.ThrowsAsync<InvariantViolationException>(() => env.Service.ArchiveNoteAsync(9999));
    }

    [Fact]
    public async Task ArchiveNoteAsync_Throws_ClosedYearException_ForLockedClosedYear()
    {
        using var env = new Env();
        var seed = await SeedAsync(env, SchoolYearStatus.Closed);
        var noteId = await SeedNoteAsync(env, seed);

        await Assert.ThrowsAsync<ClosedYearException>(() => env.Service.ArchiveNoteAsync(noteId));

        Assert.Null((await LoadNoteAsync(env, noteId)).ArchivedOn);
    }

    [Fact]
    public async Task ArchiveNoteAsync_DoesNotDeleteTheRow()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);
        var noteId = await SeedNoteAsync(env, seed, "keep me");

        await env.Service.ArchiveNoteAsync(noteId);

        Assert.Equal(1, await NoteCountAsync(env));
        Assert.Equal("keep me", (await LoadNoteAsync(env, noteId)).Text);
    }

    [Fact]
    public async Task RestoreNoteAsync_ClearsArchivedOn()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);
        var noteId = await SeedNoteAsync(env, seed, archivedOn: Now.AddDays(-1));

        await env.Service.RestoreNoteAsync(noteId);

        var stored = await LoadNoteAsync(env, noteId);
        Assert.Null(stored.ArchivedOn);
        Assert.Null(stored.LastEditedOn);
    }

    [Fact]
    public async Task RestoreNoteAsync_Throws_WhenNotArchived()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);
        var noteId = await SeedNoteAsync(env, seed);

        await Assert.ThrowsAsync<InvariantViolationException>(() => env.Service.RestoreNoteAsync(noteId));
    }

    [Fact]
    public async Task RestoreNoteAsync_Throws_ClosedYearException_ForLockedClosedYear()
    {
        using var env = new Env();
        var seed = await SeedAsync(env, SchoolYearStatus.Closed);
        var archivedOn = Now.AddDays(-1);
        var noteId = await SeedNoteAsync(env, seed, archivedOn: archivedOn);

        await Assert.ThrowsAsync<ClosedYearException>(() => env.Service.RestoreNoteAsync(noteId));

        Assert.Equal(archivedOn, (await LoadNoteAsync(env, noteId)).ArchivedOn);
    }

    // ---------- GetNotesForStudentAsync ----------

    [Fact]
    public async Task GetNotesForStudentAsync_ReturnsNewestFirst()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);
        await SeedNoteAsync(env, seed, "middle", Now.AddDays(-2));
        await SeedNoteAsync(env, seed, "newest", Now);
        await SeedNoteAsync(env, seed, "oldest", Now.AddDays(-5));

        var notes = await env.Service.GetNotesForStudentAsync(seed.StudentId);

        Assert.Equal(["newest", "middle", "oldest"], notes.Select(n => n.Text));
    }

    [Fact]
    public async Task GetNotesForStudentAsync_ExcludesArchived_ByDefault()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);
        await SeedNoteAsync(env, seed, "active");
        await SeedNoteAsync(env, seed, "archived", archivedOn: Now);

        var notes = await env.Service.GetNotesForStudentAsync(seed.StudentId);

        Assert.Equal("active", Assert.Single(notes).Text);
    }

    [Fact]
    public async Task GetNotesForStudentAsync_IncludesArchived_WhenRequested()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);
        await SeedNoteAsync(env, seed, "active");
        await SeedNoteAsync(env, seed, "archived", archivedOn: Now);

        var notes = await env.Service.GetNotesForStudentAsync(seed.StudentId, includeArchived: true);

        Assert.Equal(2, notes.Count);
        Assert.Single(notes, n => n.IsArchived);
    }

    [Fact]
    public async Task GetNotesForStudentAsync_FiltersBySchoolYear()
    {
        using var env = new Env();
        var older = await SeedAsync(env, SchoolYearStatus.Closed, label: "2024/25", startYear: 2024);
        var current = await SeedAsync(env);

        // One student enrolled in both years: enroll the current-year student in the older year too.
        await using (var context = env.SeedContext())
        {
            context.Enrollments.Add(new Enrollment
            {
                StudentId = current.StudentId,
                ClassInstanceId = older.ClassInstanceId,
                JoinedOn = new DateTime(2024, 8, 1),
            });
            await context.SaveChangesAsync();
        }

        await SeedNoteAsync(env, older, "old year", studentId: current.StudentId);
        await SeedNoteAsync(env, current, "current year");

        var notes = await env.Service.GetNotesForStudentAsync(current.StudentId, current.YearId);

        Assert.Equal("current year", Assert.Single(notes).Text);
    }

    [Fact]
    public async Task GetNotesForStudentAsync_FiltersByRange_FromInclusiveToExclusive()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);
        var from = new DateTime(2026, 2, 1);
        var to = new DateTime(2026, 3, 1);
        await SeedNoteAsync(env, seed, "before", from.AddTicks(-1));
        await SeedNoteAsync(env, seed, "at from", from);
        await SeedNoteAsync(env, seed, "inside", from.AddDays(10));
        await SeedNoteAsync(env, seed, "just before to", to.AddTicks(-1));
        await SeedNoteAsync(env, seed, "at to", to);

        var notes = await env.Service.GetNotesForStudentAsync(seed.StudentId, from: from, to: to);

        Assert.Equal(["just before to", "inside", "at from"], notes.Select(n => n.Text));
    }

    [Fact]
    public async Task GetNotesForStudentAsync_ReturnsOnlyThatStudentsNotes()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);
        var otherId = await SeedStudentInYearAsync(env, seed, "Ben");
        await SeedNoteAsync(env, seed, "mine");
        await SeedNoteAsync(env, seed, "theirs", studentId: otherId);

        var notes = await env.Service.GetNotesForStudentAsync(seed.StudentId);

        Assert.Equal("mine", Assert.Single(notes).Text);
    }

    [Fact]
    public async Task GetNotesForStudentAsync_ReturnsEmpty_ForStudentWithoutNotes()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);

        var notes = await env.Service.GetNotesForStudentAsync(seed.StudentId);

        Assert.Empty(notes);
    }

    [Fact]
    public async Task GetNotesForStudentAsync_Throws_ForUnknownStudent()
    {
        using var env = new Env();
        await SeedAsync(env);

        await Assert.ThrowsAsync<InvariantViolationException>(() => env.Service.GetNotesForStudentAsync(9999));
    }

    [Fact]
    public async Task GetNotesForStudentAsync_Throws_WhenFromIsAfterTo()
    {
        using var env = new Env();
        var seed = await SeedAsync(env);

        await Assert.ThrowsAsync<ArgumentException>(
            () => env.Service.GetNotesForStudentAsync(
                seed.StudentId, from: new DateTime(2026, 3, 1), to: new DateTime(2026, 2, 1)));
    }

    // ---------- GetNoteYearsForStudentAsync ----------

    [Fact]
    public async Task GetNoteYearsForStudentAsync_ReturnsEnrolledYearsNewestFirst()
    {
        using var env = new Env();
        var older = await SeedAsync(env, SchoolYearStatus.Closed, label: "2024/25", startYear: 2024);
        var current = await SeedAsync(env);
        await SeedAsync(env, label: "2023/24", startYear: 2023, firstName: "Unrelated");
        await using (var context = env.SeedContext())
        {
            context.Enrollments.Add(new Enrollment
            {
                StudentId = current.StudentId,
                ClassInstanceId = older.ClassInstanceId,
                JoinedOn = new DateTime(2024, 8, 1),
            });
            await context.SaveChangesAsync();
        }

        var years = await env.Service.GetNoteYearsForStudentAsync(current.StudentId);

        Assert.Equal(["2025/26", "2024/25"], years.Select(y => y.Label));
        Assert.Equal([current.YearId, older.YearId], years.Select(y => y.SchoolYearId));
    }

    [Fact]
    public async Task GetNoteYearsForStudentAsync_MarksLockedClosedYearNotWritable()
    {
        using var env = new Env();
        var seed = await SeedAsync(env, SchoolYearStatus.Closed);

        var year = Assert.Single(await env.Service.GetNoteYearsForStudentAsync(seed.StudentId));

        Assert.False(year.IsWritable);
        Assert.Equal(SchoolYearStatus.Closed, year.Status);
    }

    [Fact]
    public async Task GetNoteYearsForStudentAsync_MarksUnlockedClosedYearWritable()
    {
        using var env = new Env();
        var seed = await SeedAsync(env, SchoolYearStatus.Closed, FutureUnlock);

        var year = Assert.Single(await env.Service.GetNoteYearsForStudentAsync(seed.StudentId));

        Assert.True(year.IsWritable);
        Assert.Equal(FutureUnlock, year.UnlockedUntil);
    }
}
