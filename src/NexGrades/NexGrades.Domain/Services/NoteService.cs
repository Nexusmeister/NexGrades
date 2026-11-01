using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Auditing;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Time;

namespace NexGrades.Domain.Services;

/// <summary>
/// Creates, edits, archives and lists the free-text notes a teacher keeps on a student within a school year.
/// </summary>
/// <remarks>
/// Takes a context factory rather than a context and opens a short-lived context per call, so the service can
/// be registered as a plain transient and reused by other callers (for example a later note export) without
/// sharing a long-lived, stale change tracker. Writes to closed years are rejected and audited by
/// <see cref="ClosedYearAuditInterceptor"/>; this service only adds the note-specific rules. Notes are never
/// deleted: archiving sets <see cref="Note.ArchivedOn"/> and can be undone.
/// </remarks>
public sealed class NoteService(IDbContextFactory<AppDbContext> contextFactory, IClock clock)
{
    /// <summary>Maximum length of a note's text, after trimming.</summary>
    public const int MaxTextLength = 4000;

    /// <summary>Maximum length of a note's category, after trimming.</summary>
    public const int MaxCategoryLength = 100;

    /// <summary>
    /// Adds a note for <paramref name="studentId"/> in <paramref name="schoolYearId"/>. The note records the
    /// class the student was enrolled in for that year.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// The text or category is invalid, the student or year does not exist, or the student has no enrollment
    /// in that year.
    /// </exception>
    /// <exception cref="ClosedYearException">The year is closed and not unlocked.</exception>
    public Task<NoteDto> AddNoteAsync(
        int studentId,
        int schoolYearId,
        string text,
        string? category = null,
        CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("NoteService.AddNote", async () =>
        {
            var normalizedText = NormalizeText(text);
            var normalizedCategory = NormalizeCategory(category);

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            if (!await context.Students.AnyAsync(s => s.Id == studentId, cancellationToken))
            {
                throw new InvariantViolationException($"Student {studentId} does not exist.");
            }

            var schoolYear = await context.SchoolYears
                .FirstOrDefaultAsync(sy => sy.Id == schoolYearId, cancellationToken)
                ?? throw new InvariantViolationException($"School year {schoolYearId} does not exist.");

            // LeftOn is deliberately ignored: a student who left the class later in the year still has history there.
            var enrollment = await context.Enrollments
                .AsNoTracking()
                .Where(e => e.StudentId == studentId && e.ClassInstance.SchoolYearId == schoolYearId)
                .OrderByDescending(e => e.JoinedOn)
                .Select(e => new { e.ClassInstanceId, e.ClassInstance.Name })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvariantViolationException(
                    $"Student {studentId} has no enrollment in school year {schoolYearId}.");

            var note = new Note
            {
                StudentId = studentId,
                SchoolYearId = schoolYearId,
                ClassInstanceId = enrollment.ClassInstanceId,
                Category = normalizedCategory,
                Text = normalizedText,
                CreatedOn = clock.Now,
            };
            context.Notes.Add(note);
            await context.SaveChangesAsync(cancellationToken);

            return ToDto(note, schoolYear.Label, enrollment.Name);
        });

    /// <summary>
    /// Replaces the text and category of an active note. Saving identical values is a no-op and does not
    /// touch <see cref="Note.LastEditedOn"/>.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// The note does not exist or is archived, or the text or category is invalid.
    /// </exception>
    /// <exception cref="ClosedYearException">The note's year is closed and not unlocked.</exception>
    public Task<NoteDto> EditNoteAsync(
        int noteId, string text, string? category = null, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("NoteService.EditNote", async () =>
        {
            var normalizedText = NormalizeText(text);
            var normalizedCategory = NormalizeCategory(category);

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var note = await LoadNoteAsync(context, noteId, cancellationToken);

            if (note.ArchivedOn is not null)
            {
                throw new InvariantViolationException($"Note {noteId} is archived; restore it first before editing.");
            }

            if (note.Text == normalizedText && note.Category == normalizedCategory)
            {
                return ToDto(note);
            }

            note.Text = normalizedText;
            note.Category = normalizedCategory;
            note.LastEditedOn = clock.Now;
            await context.SaveChangesAsync(cancellationToken);

            return ToDto(note);
        });

    /// <summary>Archives an active note, hiding it from default listings. Reversible via <see cref="RestoreNoteAsync"/>.</summary>
    /// <exception cref="InvariantViolationException">The note does not exist or is already archived.</exception>
    /// <exception cref="ClosedYearException">The note's year is closed and not unlocked.</exception>
    public Task ArchiveNoteAsync(int noteId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("NoteService.ArchiveNote", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var note = await LoadNoteAsync(context, noteId, cancellationToken);

            if (note.ArchivedOn is not null)
            {
                throw new InvariantViolationException($"Note {noteId} is already archived.");
            }

            note.ArchivedOn = clock.Now;
            await context.SaveChangesAsync(cancellationToken);
        });

    /// <summary>Restores an archived note. <see cref="Note.LastEditedOn"/> is left untouched.</summary>
    /// <exception cref="InvariantViolationException">The note does not exist or is not archived.</exception>
    /// <exception cref="ClosedYearException">The note's year is closed and not unlocked.</exception>
    public Task RestoreNoteAsync(int noteId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("NoteService.RestoreNote", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var note = await LoadNoteAsync(context, noteId, cancellationToken);

            if (note.ArchivedOn is null)
            {
                throw new InvariantViolationException($"Note {noteId} is not archived.");
            }

            note.ArchivedOn = null;
            await context.SaveChangesAsync(cancellationToken);
        });

    /// <summary>
    /// Lists a student's notes, newest first. The <paramref name="from"/>/<paramref name="to"/> window filters
    /// on <see cref="Note.CreatedOn"/>: <paramref name="from"/> is inclusive and <paramref name="to"/> is
    /// exclusive.
    /// </summary>
    /// <param name="schoolYearId">Restricts the result to one school year when set.</param>
    /// <param name="includeArchived">Whether archived notes are returned as well.</param>
    /// <exception cref="InvariantViolationException">The student does not exist.</exception>
    /// <exception cref="ArgumentException"><paramref name="from"/> is later than <paramref name="to"/>.</exception>
    public Task<IReadOnlyList<NoteDto>> GetNotesForStudentAsync(
        int studentId,
        int? schoolYearId = null,
        DateTime? from = null,
        DateTime? to = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync<IReadOnlyList<NoteDto>>("NoteService.GetNotesForStudent", async () =>
        {
            if (from is { } start && to is { } end && start > end)
            {
                throw new ArgumentException($"'from' ({start:O}) must not be later than 'to' ({end:O}).", nameof(from));
            }

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await EnsureStudentExistsAsync(context, studentId, cancellationToken);

            var query = context.Notes.AsNoTracking().Where(n => n.StudentId == studentId);

            if (schoolYearId is { } yearId)
            {
                query = query.Where(n => n.SchoolYearId == yearId);
            }

            if (from is { } fromValue)
            {
                query = query.Where(n => n.CreatedOn >= fromValue);
            }

            if (to is { } toValue)
            {
                query = query.Where(n => n.CreatedOn < toValue);
            }

            if (!includeArchived)
            {
                query = query.Where(n => n.ArchivedOn == null);
            }

            return await query
                .OrderByDescending(n => n.CreatedOn)
                .ThenByDescending(n => n.Id)
                .Select(n => new NoteDto(
                    n.Id,
                    n.StudentId,
                    n.SchoolYearId,
                    n.SchoolYear.Label,
                    n.ClassInstanceId,
                    n.ClassInstance != null ? n.ClassInstance.Name : null,
                    n.Category,
                    n.Text,
                    n.CreatedOn,
                    n.LastEditedOn,
                    n.ArchivedOn))
                .ToListAsync(cancellationToken);
        });

    /// <summary>
    /// The school years the student is enrolled in, newest first, with whether each can currently be written to.
    /// These are the only years a note can be added for.
    /// </summary>
    /// <exception cref="InvariantViolationException">The student does not exist.</exception>
    public Task<IReadOnlyList<NoteYear>> GetNoteYearsForStudentAsync(
        int studentId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync<IReadOnlyList<NoteYear>>("NoteService.GetNoteYearsForStudent", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await EnsureStudentExistsAsync(context, studentId, cancellationToken);

            var years = await context.Enrollments
                .AsNoTracking()
                .Where(e => e.StudentId == studentId)
                .Select(e => e.ClassInstance.SchoolYear)
                .Distinct()
                .OrderByDescending(sy => sy.StartDate)
                .ToListAsync(cancellationToken);

            var now = clock.Now;
            return years
                .Select(sy => new NoteYear(sy.Id, sy.Label, sy.Status, sy.UnlockedUntil, SchoolYearLock.IsWritable(sy, now)))
                .ToList();
        });

    private static async Task EnsureStudentExistsAsync(AppDbContext context, int studentId, CancellationToken cancellationToken)
    {
        if (!await context.Students.AsNoTracking().AnyAsync(s => s.Id == studentId, cancellationToken))
        {
            throw new InvariantViolationException($"Student {studentId} does not exist.");
        }
    }

    private static async Task<Note> LoadNoteAsync(AppDbContext context, int noteId, CancellationToken cancellationToken) =>
        await context.Notes
            .Include(n => n.SchoolYear)
            .Include(n => n.ClassInstance)
            .FirstOrDefaultAsync(n => n.Id == noteId, cancellationToken)
        ?? throw new InvariantViolationException($"Note {noteId} does not exist.");

    private static NoteDto ToDto(Note note) =>
        ToDto(note, note.SchoolYear.Label, note.ClassInstance?.Name);

    private static NoteDto ToDto(Note note, string schoolYearLabel, string? classInstanceName) =>
        new(
            note.Id,
            note.StudentId,
            note.SchoolYearId,
            schoolYearLabel,
            note.ClassInstanceId,
            classInstanceName,
            note.Category,
            note.Text,
            note.CreatedOn,
            note.LastEditedOn,
            note.ArchivedOn);

    private static string NormalizeText(string text)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new InvariantViolationException("A note's text must not be empty.");
        }

        if (trimmed.Length > MaxTextLength)
        {
            throw new InvariantViolationException(
                $"A note's text must not exceed {MaxTextLength} characters (was {trimmed.Length}).");
        }

        return trimmed;
    }

    private static string? NormalizeCategory(string? category)
    {
        var trimmed = category?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > MaxCategoryLength)
        {
            throw new InvariantViolationException(
                $"A note's category must not exceed {MaxCategoryLength} characters (was {trimmed.Length}).");
        }

        return trimmed;
    }
}
