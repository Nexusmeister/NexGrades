using System.Globalization;
using NexGrades.App.Features.SchoolYears;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;

namespace NexGrades.App.Tests.Features.SchoolYears;

/// <summary>
/// Checks that the school year flow's messages follow the UI language. The suite is pinned to en-US, so each test
/// switches to German itself and restores the culture afterwards.
/// </summary>
public class SchoolYearsGermanTextTests
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
    public void ClassChoiceRow_DescribesTheClassInGerman_ForGermanUiCulture()
    {
        InGerman(() =>
        {
            var pending = new ClassChoiceRow(new ClassRolloverState(1, "5A", ClassInstanceStatus.Active, 3, "6A", null, null, false));
            var done = new ClassChoiceRow(new ClassRolloverState(1, "5A", ClassInstanceStatus.RolledForward, 3, "6A", 2, "6A", false));

            Assert.Equal("3 Schülerinnen und Schüler werden übernommen.", pending.CarryOverText);
            Assert.Equal("In das nächste Schuljahr übernommen als 6A.", done.DoneText);
            Assert.StartsWith("Diese Klasse konnte nicht", pending.FailedText, StringComparison.Ordinal);
        });
    }
}
