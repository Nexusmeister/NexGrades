using System.Globalization;
using NexGrades.App.Features.SchoolYears;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;

namespace NexGrades.App.Tests;

public class ClassChoiceRowTests
{
    private static ClassChoiceRow CreateRow(int carryOver) =>
        new(new ClassRolloverState(4711, "5A", ClassInstanceStatus.Active, carryOver, "6A", null, null, false));

    [Fact]
    public void CarryOverText_UsesSingularWording_ForOneStudent()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            var text = CreateRow(1).CarryOverText;

            Assert.Contains("1", text, StringComparison.Ordinal);
            Assert.DoesNotContain("1 students", text, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void CarryOverText_UsesPluralWording_ForSeveralStudents()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            var text = CreateRow(3).CarryOverText;

            Assert.Contains("3 students", text, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
