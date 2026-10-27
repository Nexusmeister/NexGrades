using ClosedXML.Excel;

namespace NexGrades.Common.Export;

/// <summary>One column of an <see cref="ExportTable"/>: its header and an optional Excel number format.</summary>
/// <param name="Header">Header text, already localized by the caller.</param>
/// <param name="NumberFormat">Excel number format applied to numeric cells (e.g. <c>0.00</c>); culture-neutral.</param>
public sealed record ExportColumn(string Header, string? NumberFormat = null);

/// <summary>
/// A plain, library-free description of one worksheet, so feature code (subject overview, student notes, ...)
/// can describe what to export without touching ClosedXML.
/// </summary>
/// <param name="SheetName">Worksheet name; sanitized to Excel's rules (31 chars, no <c>[]:*?/\</c>).</param>
/// <param name="Columns">Column headers and formats.</param>
/// <param name="Rows">
/// One list per row, aligned to <paramref name="Columns"/>. Supported cell values: <see langword="null"/>,
/// <see cref="string"/>, <see cref="decimal"/>, <see cref="int"/>, <see cref="long"/>, <see cref="double"/>,
/// <see cref="DateTime"/>, <see cref="DateOnly"/> and <see cref="bool"/>.
/// </param>
public sealed record ExportTable(string SheetName, IReadOnlyList<ExportColumn> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows);

/// <summary>
/// Writes <see cref="ExportTable"/>s to an .xlsx workbook. Kept generic on purpose so other exports reuse it.
/// Numbers are written as real numeric cells (Excel displays them in the viewer's locale); text is written
/// verbatim as text, never interpreted as a formula.
/// </summary>
public static class ExcelExporter
{
    private const int MaxSheetNameLength = 31;
    private static readonly char[] InvalidSheetNameChars = ['[', ']', ':', '*', '?', '/', '\\'];

    /// <summary>Writes a workbook with one worksheet per table to <paramref name="destination"/>.</summary>
    public static void Write(Stream destination, params ExportTable[] tables)
    {
        using var workbook = new XLWorkbook();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables)
        {
            var sheet = workbook.Worksheets.Add(UniqueSheetName(table.SheetName, usedNames));
            WriteTable(sheet, table);
        }

        if (tables.Length == 0)
        {
            workbook.Worksheets.Add("Sheet1");
        }

        workbook.SaveAs(destination);
    }

    private static void WriteTable(IXLWorksheet sheet, ExportTable table)
    {
        for (var c = 0; c < table.Columns.Count; c++)
        {
            var header = sheet.Cell(1, c + 1);
            SetValue(header, table.Columns[c].Header);
            header.Style.Font.Bold = true;
        }

        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            for (var c = 0; c < table.Columns.Count && c < row.Count; c++)
            {
                var cell = sheet.Cell(r + 2, c + 1);
                SetValue(cell, row[c]);
                if (table.Columns[c].NumberFormat is { } format && row[c] is decimal or double or int or long)
                {
                    cell.Style.NumberFormat.Format = format;
                }
            }
        }

        if (table.Columns.Count > 0)
        {
            sheet.SheetView.FreezeRows(1);
            sheet.Columns().AdjustToContents();
        }
    }

    private static void SetValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case string s:
                cell.Style.NumberFormat.Format = "@";
                cell.SetValue(s);
                break;
            case decimal d: cell.SetValue(d); break;
            case int i: cell.SetValue(i); break;
            case long l: cell.SetValue(l); break;
            case double db: cell.SetValue(db); break;
            case DateTime dt: cell.SetValue(dt); break;
            case DateOnly d: cell.SetValue(d.ToDateTime(TimeOnly.MinValue)); break;
            case bool b: cell.SetValue(b); break;
            default: throw new ArgumentException($"Unsupported export cell type {value.GetType().Name}.", nameof(value));
        }
    }

    private static string UniqueSheetName(string requested, HashSet<string> used)
    {
        var cleaned = new string(requested.Select(ch => InvalidSheetNameChars.Contains(ch) ? '_' : ch).ToArray()).Trim('\'', ' ');
        if (cleaned.Length == 0)
        {
            cleaned = "Sheet";
        }

        var name = cleaned.Length > MaxSheetNameLength ? cleaned[..MaxSheetNameLength] : cleaned;
        for (var n = 2; !used.Add(name); n++)
        {
            var suffix = $" ({n})";
            name = cleaned[..Math.Min(cleaned.Length, MaxSheetNameLength - suffix.Length)] + suffix;
        }

        return name;
    }
}
