using System.Globalization;
using NexGrades.App.Features.Grades;
using NexGrades.App.Localization;
using NexGrades.Domain.Grading;

namespace NexGrades.App.Tests.Features.Grades;

/// <summary>
/// Checks that the grade sheet's cell and row descriptions follow the UI language. The suite is pinned to en-US, so
/// each test switches to German itself and restores the culture afterwards.
/// </summary>
public class GradesGermanTextTests
{
    private static void InGerman(Action body)
    {
        var ui = CultureInfo.CurrentUICulture;
        var format = CultureInfo.CurrentCulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            body();
        }
        finally
        {
            CultureInfo.CurrentUICulture = ui;
            CultureInfo.CurrentCulture = format;
        }
    }

    [Fact]
    public void GradeCellDescription_IsGerman_ForGermanUiCulture()
    {
        InGerman(() =>
        {
            var cell = new GradeCellViewModel(
                1, new GradeSheetCell(AssessmentId: 10, State: GradeSheetCellState.Exempt, Grade: null), false, true, _ => { });

            Assert.Equal("Die Schülerin / der Schüler ist von diesem Fach befreit.", cell.Description);

            cell.HasError = true;

            Assert.StartsWith("Diese Note ist nicht gespeichert.", cell.Description, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void FinalGradeCellDescription_IsGerman_ForGermanUiCulture()
    {
        InGerman(() =>
        {
            var cell = new FinalGradeCellViewModel(1, false, 3m, 2m, true, _ => { });

            Assert.Equal("Diese Zeugnisnote überschreibt den Vorschlag 2.", cell.Description);
        });
    }

    [Fact]
    public void Format_UsesGermanDecimalComma_ForAnAverageInTheGermanCulture()
    {
        InGerman(() =>
        {
            var text = TextResources.Format("Students_StudentPage_Average_Format", GradeAverageFormatter.Format(2.5m));

            Assert.Equal("Durchschnitt: 2,50", text);
        });
    }

    [Fact]
    public void GradeSheetRow_WritesGermanStatusText_ForAnExemptStudent()
    {
        InGerman(() =>
        {
            var student = new GradeSheetStudent(
                StudentId: 1,
                FirstName: "Ada",
                LastName: "Lovelace",
                JoinedOn: new DateOnly(2025, 8, 1),
                LeftOn: null,
                IsExempt: true,
                Cells: [],
                UnassignedGrades: [],
                BucketAverages: new Dictionary<int, decimal?>(),
                SubjectAverage: null,
                FinalGradeOverride: null);

            var row = new GradeSheetRow(student, [], true, _ => { }, _ => { });

            Assert.Equal("Von diesem Fach befreit.", row.StatusText);
            Assert.Equal("Lovelace, Ada", row.StudentName);
        });
    }
}
