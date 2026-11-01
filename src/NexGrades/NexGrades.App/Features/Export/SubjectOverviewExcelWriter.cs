using System.Globalization;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using NexGrades.Domain.Export;

namespace NexGrades.App.Features.Export;

/// <summary>
/// Writes a <see cref="SubjectOverviewTable"/> as an .xlsx workbook with a single sheet. This is the only place
/// that knows ClosedXML and the header wording of the export.
/// </summary>
public sealed class SubjectOverviewExcelWriter
{
    private const string StudentHeader = "Student";
    private const string SubjectAverageHeader = "Subject average";
    private const string AverageNumberFormat = "0.0";
    private const string TextNumberFormat = "@";

    private readonly CompositeFormat _bucketGradesHeaderFormat = CompositeFormat.Parse("{0}");
    private readonly CompositeFormat _bucketAverageHeaderFormat = CompositeFormat.Parse("{0} average");
    private readonly CompositeFormat _leftStudentFormat = CompositeFormat.Parse("{0} (left on {1:d})");

    /// <summary>Writes the workbook for <paramref name="table"/> to <paramref name="output"/>.</summary>
    /// <param name="table">The table to write.</param>
    /// <param name="output">The stream that receives the .xlsx content.</param>
    /// <param name="uiCulture">The culture for headers and the date in the "left on" marker.</param>
    public void Write(SubjectOverviewTable table, Stream output, CultureInfo uiCulture)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(
            ExcelSheetName.Sanitize(table.SubjectName, table.ClassName, table.SchoolYearLabel));

        for (var c = 0; c < table.Columns.Count; c++)
        {
            var header = sheet.Cell(1, c + 1);
            SetText(header, HeaderFor(table.Columns[c], uiCulture));
            header.Style.Font.Bold = true;
        }

        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            for (var c = 0; c < table.Columns.Count; c++)
            {
                var cell = sheet.Cell(r + 2, c + 1);
                var value = row.Cells[c];

                if (value.Number is { } number)
                {
                    cell.Style.NumberFormat.Format = AverageNumberFormat;
                    cell.Value = (double)number;
                }
                else if (value.Text is { } text)
                {
                    if (c == 0 && row.LeftOn is { } leftOn)
                    {
                        text = string.Format(uiCulture, _leftStudentFormat, text, leftOn);
                    }

                    SetText(cell, text);
                }
            }
        }

        sheet.SheetView.FreezeRows(1);
        sheet.SheetView.FreezeColumns(1);
        sheet.Columns().AdjustToContents();

        workbook.SaveAs(output);
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

    private string HeaderFor(SubjectOverviewColumn column, CultureInfo uiCulture) => column.Kind switch
    {
        SubjectOverviewColumnKind.Student => StudentHeader,
        SubjectOverviewColumnKind.BucketGrades => string.Format(uiCulture, _bucketGradesHeaderFormat, column.BucketName),
        SubjectOverviewColumnKind.BucketAverage => string.Format(uiCulture, _bucketAverageHeaderFormat, column.BucketName),
        SubjectOverviewColumnKind.SubjectAverage => SubjectAverageHeader,
        _ => throw new ArgumentOutOfRangeException(nameof(column), column.Kind, "Unknown column kind."),
    };
}
