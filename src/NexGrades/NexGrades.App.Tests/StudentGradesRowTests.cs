using System.Globalization;
using NexGrades.App.Features.Students;
using NexGrades.Domain.Grading;

namespace NexGrades.App.Tests;

public class StudentGradesRowTests
{
    private static readonly DateTime VoidedAt = new(2025, 11, 1, 9, 0, 0);

    private static void InCulture(string culture, Action body)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            body();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static StudentGradeEntry Entry(
        DateTime? voidedOn = null, bool assessmentVoided = false, string token = "2-") =>
        new(1, 10, null, null, 2.3m, token, new DateOnly(2025, 9, 15), null, null, voidedOn, assessmentVoided);

    private static StudentSubjectGrades Subject(decimal? subjectAverage, decimal? bucketAverage, decimal weight = 62.5m) =>
        new(
            5,
            "Testkunde",
            false,
            [new StudentBucketGrades(10, "Written", weight, [], bucketAverage, [Entry()])],
            subjectAverage);

    [Fact]
    public void StudentSubjectRow_FormatsAveragesWithDecimalComma_InDeDeCulture()
    {
        InCulture("de-DE", () =>
        {
            var row = new StudentSubjectRow(Subject(2.35m, 1.5m));

            Assert.Equal("2,35", row.SubjectAverageText);
            Assert.Equal("1,50", row.Buckets[0].AverageText);
        });
    }

    [Fact]
    public void StudentSubjectRow_FormatsAveragesWithDecimalPoint_InEnUsCulture()
    {
        InCulture("en-US", () =>
        {
            var row = new StudentSubjectRow(Subject(2.35m, 1.5m));

            Assert.Equal("2.35", row.SubjectAverageText);
        });
    }

    [Fact]
    public void StudentSubjectRow_ShowsAnEnDash_ForMissingAverages()
    {
        InCulture("de-DE", () =>
        {
            var row = new StudentSubjectRow(Subject(null, null));

            Assert.Equal("–", row.SubjectAverageText);
            Assert.Equal("–", row.Buckets[0].AverageText);
        });
    }

    [Fact]
    public void StudentBucketRow_FormatsTheWeightWithTheCurrentCulture()
    {
        InCulture("de-DE", () =>
        {
            var row = new StudentSubjectRow(Subject(null, null, weight: 62.5m));

            Assert.Equal("Weight 62,5%", row.Buckets[0].WeightText);
        });
    }

    [Fact]
    public void StudentGradeRow_FormatsTheDateWithTheCurrentCulture()
    {
        InCulture("de-DE", () => Assert.Equal("15.09.2025", new StudentGradeRow(Entry(), false).DateText));
    }

    [Fact]
    public void StudentGradeRow_KeepsTheTokenUnchanged_InAnyCulture()
    {
        InCulture("de-DE", () => Assert.Equal("2-", new StudentGradeRow(Entry(), false).Token));
    }

    [Fact]
    public void StudentSubjectRow_PassesExemptionToBucketsAndGradeRows()
    {
        var subject = new StudentSubjectGrades(
            5, "Testkunde", true, [new StudentBucketGrades(10, "Written", 100m, [], null, [Entry()])], null);

        var row = new StudentSubjectRow(subject);

        Assert.True(row.IsExempt);
        Assert.True(row.Buckets[0].IsExempt);
        Assert.False(row.Buckets[0].Grades[0].CanEdit);
        Assert.False(row.Buckets[0].HasNoGrades);
    }

    [Fact]
    public void StudentGradeRow_AllowsEditVoidAndNotRestore_ForACountingGrade()
    {
        var row = new StudentGradeRow(Entry(), false);

        Assert.True(row.CanEdit);
        Assert.True(row.CanVoid);
        Assert.False(row.CanRestore);
        Assert.False(row.IsVoided);
    }

    [Fact]
    public void StudentGradeRow_AllowsOnlyRestore_ForAVoidedGrade()
    {
        var row = new StudentGradeRow(Entry(voidedOn: VoidedAt), false);

        Assert.False(row.CanEdit);
        Assert.False(row.CanVoid);
        Assert.True(row.CanRestore);
        Assert.True(row.IsVoided);
        Assert.NotNull(row.VoidedText);
    }

    [Fact]
    public void StudentGradeRow_AllowsNothing_ForAGradeOfAVoidedAssessment()
    {
        var row = new StudentGradeRow(Entry(assessmentVoided: true), false);

        Assert.True(row.IsVoided);
        Assert.False(row.CanEdit);
        Assert.False(row.CanVoid);
        Assert.False(row.CanRestore);
    }

    [Fact]
    public void StudentGradeRow_AllowsVoidButNotEditOrRestore_ForAnExemptStudent()
    {
        var counting = new StudentGradeRow(Entry(), true);
        var voided = new StudentGradeRow(Entry(voidedOn: VoidedAt), true);

        Assert.False(counting.CanEdit);
        Assert.True(counting.CanVoid);
        Assert.False(voided.CanRestore);
    }

    [Fact]
    public void MarkVoided_BlocksFurtherVoiding_AndMarkRestoredReopensIt()
    {
        var row = new StudentGradeRow(Entry(), false);

        row.MarkVoided(VoidedAt);
        Assert.False(row.CanVoid);
        Assert.True(row.CanRestore);

        row.MarkRestored();
        Assert.True(row.CanVoid);
        Assert.False(row.CanRestore);
        Assert.Null(row.VoidedText);
    }
}
