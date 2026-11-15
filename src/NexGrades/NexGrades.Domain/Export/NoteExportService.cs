using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;
using NexGrades.Domain.Time;

namespace NexGrades.Domain.Export;

/// <summary>
/// Collects one student's notes in a date range for export. Read-only, so it also works on closed years; the range
/// may span several school years.
/// </summary>
/// <param name="contextFactory">Creates the short-lived context that each call opens.</param>
/// <param name="clock">Source of the export date.</param>
/// <remarks>
/// The range filters on <see cref="Data.Entities.Note.CreatedOn"/>. Archived notes are left out unless asked for.
/// </remarks>
public sealed class NoteExportService(IDbContextFactory<AppDbContext> contextFactory, IClock clock)
{
    /// <summary>Builds the notes table for <paramref name="studentId"/>, oldest note first.</summary>
    /// <param name="studentId">The student whose notes are exported.</param>
    /// <param name="from">First day of the range.</param>
    /// <param name="to">Last day of the range; the whole day is included.</param>
    /// <param name="includeArchived">Whether archived notes are included.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <exception cref="InvariantViolationException">The range is reversed or the student does not exist.</exception>
    public Task<NoteExportTable> BuildAsync(
        int studentId, DateOnly from, DateOnly to, bool includeArchived, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("NoteExportService.Build", async () =>
        {
            if (from > to)
            {
                throw new InvariantViolationException(string.Create(
                    CultureInfo.InvariantCulture, $"The export range starts on {from:yyyy-MM-dd}, after it ends on {to:yyyy-MM-dd}."));
            }

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var student = await context.Students
                .AsNoTracking()
                .Where(s => s.Id == studentId)
                .Select(s => new { s.FirstName, s.LastName })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvariantViolationException($"Student {studentId} does not exist.");

            var start = from.ToDateTime(TimeOnly.MinValue);
            var endExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

            var query = context.Notes
                .AsNoTracking()
                .Where(n => n.StudentId == studentId && n.CreatedOn >= start && n.CreatedOn < endExclusive);
            if (!includeArchived)
            {
                query = query.Where(n => n.ArchivedOn == null);
            }

            var rows = await query
                .OrderBy(n => n.CreatedOn)
                .ThenBy(n => n.Id)
                .Select(n => new NoteExportRow(
                    n.CreatedOn,
                    n.SchoolYear.Label,
                    n.ClassInstance != null ? n.ClassInstance.Name : null,
                    n.Category,
                    n.Text,
                    n.LastEditedOn,
                    n.ArchivedOn))
                .ToListAsync(cancellationToken);

            return new NoteExportTable(student.FirstName, student.LastName, from, to, includeArchived, clock.Now, rows);
        });
}
