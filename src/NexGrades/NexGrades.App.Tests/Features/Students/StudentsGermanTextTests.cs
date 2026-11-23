using System.Globalization;
using NexGrades.App.Features.Students;
using NexGrades.Domain.Grading;

namespace NexGrades.App.Tests.Features.Students;

/// <summary>
/// Checks that the student page's validation messages and grade rows follow the UI language. The suite is pinned to
/// en-US, so each test switches to German itself and restores the culture afterwards.
/// </summary>
public class StudentsGermanTextTests
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

    private static StudentGradeEntry Entry(DateTime? voidedOn = null) =>
        new(1, 10, null, null, 2.3m, "2-", new DateOnly(2025, 9, 15), null, null, voidedOn, false);

    [Fact]
    public void ExportDialogValidationMessage_IsGerman_ForGermanUiCulture()
    {
        InGerman(() =>
        {
            var viewModel = new ExportNotesDialogViewModel(new DateOnly(2025, 8, 1), new DateOnly(2026, 7, 31))
            {
                From = new DateTime(2026, 8, 1),
            };

            Assert.Equal("Das Startdatum darf nicht nach dem Enddatum liegen.", viewModel.ValidationMessage);

            viewModel.From = null;

            Assert.Equal("Geben Sie ein Startdatum und ein Enddatum ein.", viewModel.ValidationMessage);
        });
    }

    [Fact]
    public void StudentSubjectRow_DescribesTheFinalGradeInGerman_ForGermanUiCulture()
    {
        InGerman(() =>
        {
            var subject = new StudentSubjectGrades(
                5, "Mathematik", false, [new StudentBucketGrades(10, "Klassenarbeiten", 100m, [], 2.0m, [Entry()])], 2.0m, 4m);

            var row = new StudentSubjectRow(subject);

            Assert.Equal("Zeugnisnote: 4 (von der Lehrkraft festgelegt, Vorschlag 2)", row.FinalGradeDescription);
        });
    }

    [Fact]
    public void StudentBucketRow_FormatsTheWeightInGerman_WithANonBreakingSpaceBeforeThePercentSign()
    {
        InGerman(() =>
        {
            var bucket = new StudentBucketGrades(10, "Klassenarbeiten", 62.5m, [], null, [Entry()]);

            var row = new StudentBucketRow(bucket, false);

            Assert.Equal("Gewichtung 62,5 %", row.WeightText);
        });
    }

    [Fact]
    public void StudentGradeRow_WritesGermanVoidedText_ForGermanUiCulture()
    {
        InGerman(() =>
        {
            var voidedOn = new DateTime(2025, 11, 1, 9, 0, 0);

            var row = new StudentGradeRow(Entry(voidedOn), false);

            Assert.Equal(string.Format(CultureInfo.CurrentCulture, "Gestrichen am {0:g}", voidedOn), row.VoidedText);
        });
    }
}
