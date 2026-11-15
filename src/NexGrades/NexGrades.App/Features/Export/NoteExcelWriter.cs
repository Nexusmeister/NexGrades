using System.Globalization;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using NexGrades.Domain.Export;

namespace NexGrades.App.Features.Export;

/// <summary>
/// Writes a <see cref="NoteExportTable"/> as an .xlsx workbook with a single sheet: a header block about the
/// student and the range, then one row per note. This is the only place that knows ClosedXML and the header
/// wording of the notes export.
/// </summary>
public sealed class NoteExcelWriter
{
    private const string SheetName = "Notes";
    private const string StudentLabel = "Student";
    private const string RangeLabel = "Date range";
    private const string ExportedLabel = "Exported on";
    private const string DateHeader = "Date";
    private const string SchoolYearHeader = "School year";
    private const string ClassHeader = "Class";
    private const string CategoryHeader = "Category";
    private const string NoteHeader = "Note";
    private const string LastEditedHeader = "Last edited";
    private const string ArchivedHeader = "Archived on";
    private const string TextNumberFormat = "@";

    // Excel's built-in, locale-aware formats: 14 is the short date, 22 the short date with time.
    private const int ShortDateFormatId = 14;
    private const int ShortDateTimeFormatId = 22;
    private const double NoteColumnWidth = 70;
    private const int HeaderRow = 5;
    private const int NoteColumn = 5;

    private readonly CompositeFormat _studentFormat = CompositeFormat.Parse("{0}, {1}");

    /// <summary>Writes the workbook for <paramref name="table"/> to <paramref name="output"/>.</summary>
    /// <param name="table">The table to write.</param>
    /// <param name="output">The stream that receives the .xlsx content.</param>
    /// <param name="uiCulture">The culture for the student name format.</param>
    public void Write(NoteExportTable table, Stream output, CultureInfo uiCulture)
    {
        ArgumentNullException.ThrowIfNull(table);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName);

        WriteHeaderBlock(sheet, table, uiCulture);

        var headers = new List<string> { DateHeader, SchoolYearHeader, ClassHeader, CategoryHeader, NoteHeader, LastEditedHeader };
        if (table.IncludeArchived)
        {
            headers.Add(ArchivedHeader);
        }

        for (var c = 0; c < headers.Count; c++)
        {
            var cell = sheet.Cell(HeaderRow, c + 1);
            SetText(cell, headers[c]);
            cell.Style.Font.Bold = true;
        }

        for (var r = 0; r < table.Rows.Count; r++)
        {
            var note = table.Rows[r];
            var row = HeaderRow + 1 + r;

            SetDateTime(sheet.Cell(row, 1), note.CreatedOn);
            SetText(sheet.Cell(row, 2), note.SchoolYearLabel);
            SetOptionalText(sheet.Cell(row, 3), note.ClassName);
            SetOptionalText(sheet.Cell(row, 4), note.Category);

            var noteCell = sheet.Cell(row, NoteColumn);
            SetText(noteCell, note.Text);
            noteCell.Style.Alignment.WrapText = true;

            if (note.LastEditedOn is { } edited)
            {
                SetDateTime(sheet.Cell(row, 6), edited);
            }

            if (table.IncludeArchived && note.ArchivedOn is { } archived)
            {
                SetDateTime(sheet.Cell(row, 7), archived);
            }
        }

        if (table.Rows.Count > 0)
        {
            sheet.Range(HeaderRow + 1, 1, HeaderRow + table.Rows.Count, headers.Count)
                .Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        }

        sheet.SheetView.FreezeRows(HeaderRow);
        sheet.Columns().AdjustToContents();
        sheet.Column(NoteColumn).Width = NoteColumnWidth;

        workbook.SaveAs(output);
    }

    private void WriteHeaderBlock(IXLWorksheet sheet, NoteExportTable table, CultureInfo uiCulture)
    {
        SetLabel(sheet.Cell(1, 1), StudentLabel);
        SetText(sheet.Cell(1, 2), string.Format(uiCulture, _studentFormat, table.LastName, table.FirstName));

        SetLabel(sheet.Cell(2, 1), RangeLabel);
        SetDate(sheet.Cell(2, 2), table.From.ToDateTime(TimeOnly.MinValue));
        SetDate(sheet.Cell(2, 3), table.To.ToDateTime(TimeOnly.MinValue));

        SetLabel(sheet.Cell(3, 1), ExportedLabel);
        SetDate(sheet.Cell(3, 2), table.ExportedOn.Date);
    }

    private static void SetLabel(IXLCell cell, string text)
    {
        SetText(cell, text);
        cell.Style.Font.Bold = true;
    }

    private static void SetDate(IXLCell cell, DateTime value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.NumberFormatId = ShortDateFormatId;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
    }

    private static void SetDateTime(IXLCell cell, DateTime value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.NumberFormatId = ShortDateTimeFormatId;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
    }

    private static void SetOptionalText(IXLCell cell, string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            SetText(cell, text);
        }
    }

    // Text format plus an explicit text value keeps content such as "=1+1" or "'x" literal.
    private static void SetText(IXLCell cell, string text)
    {
        cell.Style.NumberFormat.Format = TextNumberFormat;

        // ClosedXML treats one leading apostrophe as Excel's quote prefix and drops it. A doubled apostrophe
        // keeps the real one, and clearing the prefix flag stops Excel from hiding it.
        if (text.StartsWith('\''))
        {
            cell.Value = (XLCellValue)("'" + text);
            cell.Style.IncludeQuotePrefix = false;
            return;
        }

        cell.Value = (XLCellValue)text;
    }
}
