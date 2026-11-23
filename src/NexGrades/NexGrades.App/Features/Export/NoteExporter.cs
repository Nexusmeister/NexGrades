using System.Globalization;
using System.IO;
using NexGrades.App.Localization;
using NexGrades.Domain.Export;

namespace NexGrades.App.Features.Export;

/// <summary>
/// Exports a student's notes to an .xlsx file: builds the table in the Domain and writes it with
/// <see cref="NoteExcelWriter"/>. Read-only with respect to the database.
/// </summary>
/// <param name="exportService">Builds the table from the database.</param>
/// <param name="writer">Writes the table as an Excel workbook.</param>
public sealed class NoteExporter(NoteExportService exportService, NoteExcelWriter writer)
{
    /// <summary>Collects the notes in the range; the table has no rows when nothing matches.</summary>
    /// <param name="studentId">The student whose notes are exported.</param>
    /// <param name="from">First day of the range.</param>
    /// <param name="to">Last day of the range; the whole day is included.</param>
    /// <param name="includeArchived">Whether archived notes are included.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <exception cref="NexGrades.Domain.Exceptions.InvariantViolationException">The range is reversed or the student does not exist.</exception>
    public Task<NoteExportTable> BuildAsync(
        int studentId, DateOnly from, DateOnly to, bool includeArchived, CancellationToken cancellationToken = default) =>
        exportService.BuildAsync(studentId, from, to, includeArchived, cancellationToken);

    /// <summary>
    /// Suggests a file name such as "Notes Miller Anna 2025-08-01–2026-07-31.xlsx". Only the leading word is
    /// localized (German: "Notizen"); the dates are always yyyy-MM-dd. Characters that are invalid in file names
    /// are replaced.
    /// </summary>
    /// <param name="table">The table to be saved.</param>
    /// <param name="uiCulture">The language of the leading word.</param>
    public static string SuggestFileName(NoteExportTable table, CultureInfo uiCulture)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(uiCulture);

        var name = string.Format(
            CultureInfo.InvariantCulture,
            TextResources.Get("Export_Notes_FileName_Format", uiCulture),
            table.LastName,
            table.FirstName,
            table.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            table.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '-' : c).ToArray();
        return new string(chars).Trim() + ".xlsx";
    }

    /// <summary>
    /// Writes <paramref name="table"/> to <paramref name="path"/>. The workbook is built in memory first, so a
    /// failure never leaves a half-written file.
    /// </summary>
    /// <exception cref="IOException">The file cannot be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The path is not writable.</exception>
    public async Task WriteAsync(NoteExportTable table, string path, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        writer.Write(table, stream, CultureInfo.CurrentUICulture);

        await File.WriteAllBytesAsync(path, stream.ToArray(), cancellationToken);
    }
}
