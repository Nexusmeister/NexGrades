using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using NexGrades.App.Features.Export;
using NexGrades.Domain.Export;

namespace NexGrades.App.Tests.Features.Export;

public class NoteExcelWriterTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    private static NoteExportRow RowOf(
        string text, DateTime createdOn, DateTime? edited = null, DateTime? archived = null, string? className = "5A", string? category = null) =>
        new(createdOn, "2025/26", className, category, text, edited, archived);

    private static NoteExportTable TableOf(bool includeArchived, params NoteExportRow[] rows) =>
        new("Test", "Alpha", new DateOnly(2025, 8, 1), new DateOnly(2026, 7, 31), includeArchived, new DateTime(2026, 3, 1, 10, 30, 0), rows);

    private static XLWorkbook Write(NoteExportTable table)
    {
        using var stream = new MemoryStream();
        new NoteExcelWriter().Write(table, stream, EnUs);
        stream.Position = 0;
        return new XLWorkbook(stream);
    }

    [Fact]
    public void Write_WritesHeaderBlock_WithStudentRangeAndExportDate()
    {
        using var workbook = Write(TableOf(false));

        var sheet = Assert.Single(workbook.Worksheets);
        Assert.Equal("Student", sheet.Cell(1, 1).GetString());
        Assert.Equal("Alpha, Test", sheet.Cell(1, 2).GetString());
        Assert.Equal("Date range", sheet.Cell(2, 1).GetString());
        Assert.Equal(new DateTime(2025, 8, 1), sheet.Cell(2, 2).GetDateTime());
        Assert.Equal(new DateTime(2026, 7, 31), sheet.Cell(2, 3).GetDateTime());
        Assert.Equal("Exported on", sheet.Cell(3, 1).GetString());
        Assert.Equal(new DateTime(2026, 3, 1), sheet.Cell(3, 2).GetDateTime());
    }

    [Fact]
    public void Write_OmitsArchivedColumn_WhenArchivedNotIncluded()
    {
        using var workbook = Write(TableOf(false, RowOf("note", new DateTime(2025, 10, 1))));

        var sheet = workbook.Worksheet(1);
        Assert.Equal(
            ["Date", "School year", "Class", "Category", "Note", "Last edited"],
            [.. Enumerable.Range(1, 6).Select(c => sheet.Cell(5, c).GetString())]);
        Assert.True(sheet.Cell(5, 7).IsEmpty());
    }

    [Fact]
    public void Write_AddsArchivedColumn_WhenArchivedIncluded()
    {
        var archivedOn = new DateTime(2025, 10, 3, 9, 15, 0);
        using var workbook = Write(TableOf(true, RowOf("note", new DateTime(2025, 10, 1), archived: archivedOn)));

        var sheet = workbook.Worksheet(1);
        Assert.Equal("Archived on", sheet.Cell(5, 7).GetString());
        Assert.Equal(XLDataType.DateTime, sheet.Cell(6, 7).DataType);
        Assert.Equal(archivedOn, sheet.Cell(6, 7).GetDateTime());
    }

    [Fact]
    public void Write_WritesDatesAsRealDateCells()
    {
        var created = new DateTime(2025, 10, 1, 8, 30, 0);
        var edited = new DateTime(2025, 10, 2, 14, 0, 0);
        using var workbook = Write(TableOf(false, RowOf("note", created, edited)));

        var sheet = workbook.Worksheet(1);
        Assert.Equal(XLDataType.DateTime, sheet.Cell(6, 1).DataType);
        Assert.Equal(created, sheet.Cell(6, 1).GetDateTime());
        Assert.Equal(XLDataType.DateTime, sheet.Cell(6, 6).DataType);
        Assert.Equal(edited, sheet.Cell(6, 6).GetDateTime());
    }

    [Fact]
    public void Write_LeavesLastEditedEmpty_ForNeverEditedNote()
    {
        using var workbook = Write(TableOf(false, RowOf("note", new DateTime(2025, 10, 1))));

        Assert.True(workbook.Worksheet(1).Cell(6, 6).IsEmpty());
    }

    [Fact]
    public void Write_WrapsNoteText_AndWritesRowValues()
    {
        using var workbook = Write(TableOf(false, RowOf("line one\nline two", new DateTime(2025, 10, 1), category: "Behaviour")));

        var sheet = workbook.Worksheet(1);
        Assert.Equal("2025/26", sheet.Cell(6, 2).GetString());
        Assert.Equal("5A", sheet.Cell(6, 3).GetString());
        Assert.Equal("Behaviour", sheet.Cell(6, 4).GetString());
        Assert.Equal("line one\nline two", sheet.Cell(6, 5).GetString());
        Assert.True(sheet.Cell(6, 5).Style.Alignment.WrapText);
    }

    [Fact]
    public void Write_LeavesClassAndCategoryEmpty_WhenNull()
    {
        using var workbook = Write(TableOf(false, RowOf("note", new DateTime(2025, 10, 1), className: null)));

        var sheet = workbook.Worksheet(1);
        Assert.True(sheet.Cell(6, 3).IsEmpty());
        Assert.True(sheet.Cell(6, 4).IsEmpty());
    }

    [Fact]
    public void Write_KeepsFormulaLikeTextLiteral()
    {
        using var workbook = Write(TableOf(false, RowOf("=1+1", new DateTime(2025, 10, 1))));

        var cell = workbook.Worksheet(1).Cell(6, 5);
        Assert.Equal("=1+1", cell.GetString());
        Assert.False(cell.HasFormula);
    }

    [Fact]
    public void Write_WritesHeaderOnly_ForEmptyTable()
    {
        using var workbook = Write(TableOf(false));

        var sheet = workbook.Worksheet(1);
        Assert.Equal("Date", sheet.Cell(5, 1).GetString());
        Assert.True(sheet.Cell(6, 1).IsEmpty());
    }

    [Fact]
    public void Write_ThrowsArgumentNull_ForNullTable() =>
        Assert.Throws<ArgumentNullException>(() => new NoteExcelWriter().Write(null!, new MemoryStream(), EnUs));
}
