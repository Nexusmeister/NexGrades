using System.Globalization;
using System.IO;
using NexGrades.Domain.Export;

namespace NexGrades.App.Features.Export;

/// <summary>
/// Exports the class overview of a subject instance to an .xlsx file: builds the table in the Domain and writes
/// it with <see cref="SubjectOverviewExcelWriter"/>. Read-only with respect to the database.
/// </summary>
/// <param name="exportService">Builds the table from the database.</param>
/// <param name="writer">Writes the table as an Excel workbook.</param>
public sealed class SubjectOverviewExporter(SubjectOverviewExportService exportService, SubjectOverviewExcelWriter writer)
{
    /// <summary>
    /// Suggests a file name such as "Mathematics 5A 2025-26.xlsx". Characters that are invalid in file names
    /// (the year label "2025/26" contains one) are replaced.
    /// </summary>
    public async Task<string> SuggestFileNameAsync(int subjectInstanceId, CancellationToken cancellationToken = default)
    {
        var table = await exportService.BuildAsync(subjectInstanceId, cancellationToken);
        return SuggestFileName(table);
    }

    /// <summary>
    /// Writes the overview of <paramref name="subjectInstanceId"/> to <paramref name="path"/>. The workbook is
    /// built in memory first, so a failure never leaves a half-written file.
    /// </summary>
    /// <exception cref="IOException">The file cannot be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The path is not writable.</exception>
    public async Task ExportAsync(int subjectInstanceId, string path, CancellationToken cancellationToken = default)
    {
        var table = await exportService.BuildAsync(subjectInstanceId, cancellationToken);

        using var stream = new MemoryStream();
        writer.Write(table, stream, CultureInfo.CurrentUICulture);

        await File.WriteAllBytesAsync(path, stream.ToArray(), cancellationToken);
    }

    private static string SuggestFileName(SubjectOverviewTable table)
    {
        var name = $"{table.SubjectName} {table.ClassName} {table.SchoolYearLabel}";
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim().Select(c => Array.IndexOf(invalid, c) >= 0 ? '-' : c).ToArray();
        var cleaned = new string(chars).Trim().TrimEnd('.');
        return (cleaned.Length == 0 ? ExcelSheetName.Fallback : cleaned) + ".xlsx";
    }
}
