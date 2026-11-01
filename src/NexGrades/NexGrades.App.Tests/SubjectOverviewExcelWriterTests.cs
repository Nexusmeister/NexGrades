using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using NexGrades.App.Features.Export;
using NexGrades.Domain.Export;

namespace NexGrades.App.Tests;

public class SubjectOverviewExcelWriterTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    private static readonly SubjectOverviewColumn[] Columns =
    [
        new(SubjectOverviewColumnKind.Student, null),
        new(SubjectOverviewColumnKind.BucketGrades, "Written"),
        new(SubjectOverviewColumnKind.BucketAverage, "Written"),
        new(SubjectOverviewColumnKind.SubjectAverage, null),
    ];

    private static SubjectOverviewTable TableOf(params SubjectOverviewRow[] rows) =>
        new("Testkunde", "5A", "2025/26", Columns, rows);

    private static SubjectOverviewRow RowOf(string name, string? tokens, decimal? average, DateOnly? leftOn = null) =>
        new(
            [
                SubjectOverviewCell.FromText(name),
                tokens is null ? SubjectOverviewCell.Empty : SubjectOverviewCell.FromText(tokens),
                SubjectOverviewCell.FromNumber(average),
                SubjectOverviewCell.FromNumber(average),
            ],
            leftOn);

    private static XLWorkbook Write(SubjectOverviewTable table, CultureInfo? culture = null)
    {
        using var stream = new MemoryStream();
        new SubjectOverviewExcelWriter().Write(table, stream, culture ?? EnUs);
        stream.Position = 0;
        return new XLWorkbook(stream);
    }

    [Fact]
    public void Write_CreatesExactlyOneSheet_WithSanitizedName()
    {
        using var workbook = Write(TableOf(RowOf("Alpha, Test", "2", 2.0m)));

        var sheet = Assert.Single(workbook.Worksheets);
        Assert.Equal("Testkunde 5A 2025-26", sheet.Name);
    }

    [Fact]
    public void Write_WritesHeaders_FromColumns()
    {
        using var workbook = Write(TableOf());

        var sheet = workbook.Worksheet(1);
        Assert.Equal("Student", sheet.Cell(1, 1).GetString());
        Assert.Equal("Written", sheet.Cell(1, 2).GetString());
        Assert.Equal("Written average", sheet.Cell(1, 3).GetString());
        Assert.Equal("Subject average", sheet.Cell(1, 4).GetString());
    }

    [Fact]
    public void Write_WritesAveragesAsNumericCells_WithOneDecimalFormat()
    {
        using var workbook = Write(TableOf(RowOf("Alpha, Test", "2; 3", 2.5m)));

        var sheet = workbook.Worksheet(1);
        var cell = sheet.Cell(2, 3);
        Assert.True(cell.Value.IsNumber);
        Assert.Equal(2.5, cell.Value.GetNumber());
        Assert.Equal("0.0", cell.Style.NumberFormat.Format);
        Assert.True(sheet.Cell(2, 4).Value.IsNumber);
    }

    [Fact]
    public void Write_KeepsAverageFullPrecision_AndOnlyFormatsForDisplay()
    {
        using var workbook = Write(TableOf(RowOf("Alpha, Test", "2", 2.3333m)));

        Assert.Equal(2.3333, workbook.Worksheet(1).Cell(2, 3).Value.GetNumber(), 6);
    }

    [Fact]
    public void Write_LeavesAverageCellEmpty_WhenAverageIsNull()
    {
        using var workbook = Write(TableOf(RowOf("Alpha, Test", null, null)));

        var sheet = workbook.Worksheet(1);
        Assert.True(sheet.Cell(2, 2).IsEmpty());
        Assert.True(sheet.Cell(2, 3).IsEmpty());
        Assert.True(sheet.Cell(2, 4).IsEmpty());
    }

    [Fact]
    public void Write_WritesGradeTokensAsText()
    {
        using var workbook = Write(TableOf(RowOf("Alpha, Test", "2-; 1+; 3", 2.0m)));

        var cell = workbook.Worksheet(1).Cell(2, 2);
        Assert.True(cell.Value.IsText);
        Assert.Equal("2-; 1+; 3", cell.GetString());
        Assert.Equal("@", cell.Style.NumberFormat.Format);
    }

    [Theory]
    [InlineData("=1+1, X")]
    [InlineData("+x")]
    [InlineData("-x")]
    [InlineData("@x")]
    [InlineData("'x")]
    public void Write_KeepsFormulaLikeNamesLiteral_AsText(string name)
    {
        using var workbook = Write(TableOf(RowOf(name, "2", 2.0m)));

        var cell = workbook.Worksheet(1).Cell(2, 1);
        Assert.False(cell.HasFormula);
        Assert.True(cell.Value.IsText);
        Assert.Equal(name, cell.GetString());
    }

    [Fact]
    public void Write_AppendsLeftMarker_ForStudentWhoLeft()
    {
        using var workbook = Write(TableOf(RowOf("Alpha, Test", "2", 2.0m, new DateOnly(2025, 12, 1))), EnUs);

        Assert.Equal("Alpha, Test (left on 12/1/2025)", workbook.Worksheet(1).Cell(2, 1).GetString());
    }

    [Fact]
    public void Write_FormatsLeftMarkerDate_ByUiCulture()
    {
        var table = TableOf(RowOf("Alpha, Test", "2", 2.0m, new DateOnly(2025, 12, 1)));

        using var en = Write(table, EnUs);
        using var de = Write(table, DeDe);

        var enText = en.Worksheet(1).Cell(2, 1).GetString();
        var deText = de.Worksheet(1).Cell(2, 1).GetString();
        Assert.NotEqual(enText, deText);
        Assert.Equal("Alpha, Test (left on 01.12.2025)", deText);
    }

    [Fact]
    public void Write_OmitsLeftMarker_ForActiveStudent()
    {
        using var workbook = Write(TableOf(RowOf("Alpha, Test", "2", 2.0m)));

        Assert.Equal("Alpha, Test", workbook.Worksheet(1).Cell(2, 1).GetString());
    }
}
