using System.Globalization;
using NexGrades.App.Features.SchoolYears;
using NexGrades.Domain.Services;

namespace NexGrades.App.Tests;

public class ReadinessItemTextTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    [Fact]
    public void Format_WordsClassStillActive_WithClassName()
    {
        var issue = new CloseReadinessIssue(CloseReadinessIssueKind.ClassStillActive, 4711, "5A");

        Assert.Equal("Class 5A has not been rolled forward or archived yet.", ReadinessItemText.Format(issue, EnUs));
    }

    [Fact]
    public void Format_WordsSubjectWithoutGrades_WithSubjectAndClass()
    {
        var issue = new CloseReadinessIssue(CloseReadinessIssueKind.SubjectWithoutGrades, 4711, "5A", 4712, "Subject A");

        Assert.Equal("Subject A in class 5A has no grades yet.", ReadinessItemText.Format(issue, EnUs));
    }

    [Fact]
    public void Format_WordsBucketWeights_WithTotal()
    {
        var issue = new CloseReadinessIssue(
            CloseReadinessIssueKind.BucketWeightsNotHundred, 4711, "5A", 4712, "Subject A", WeightTotal: 90m);

        Assert.Equal(
            "The grade category weights of Subject A in class 5A add up to 90 instead of 100.",
            ReadinessItemText.Format(issue, EnUs));
    }

    [Fact]
    public void Format_WordsStudentWithoutGrade_WithLastNameFirst()
    {
        var issue = new CloseReadinessIssue(
            CloseReadinessIssueKind.StudentWithoutGrade, 4711, "5A", 4712, "Subject A", 4713, "Student", "A");

        Assert.Equal("A, Student has no grade in Subject A in class 5A.", ReadinessItemText.Format(issue, EnUs));
    }

    [Fact]
    public void Format_UsesGermanDecimalSeparator_ForWeightTotal()
    {
        var issue = new CloseReadinessIssue(
            CloseReadinessIssueKind.BucketWeightsNotHundred, 4711, "5A", 4712, "Subject A", WeightTotal: 92.5m);

        Assert.Contains("92,5", ReadinessItemText.Format(issue, DeDe), StringComparison.Ordinal);
    }

    [Fact]
    public void Format_UsesInvariantStyleDecimalSeparator_ForEnglishWeightTotal()
    {
        var issue = new CloseReadinessIssue(
            CloseReadinessIssueKind.BucketWeightsNotHundred, 4711, "5A", 4712, "Subject A", WeightTotal: 92.5m);

        Assert.Contains("92.5", ReadinessItemText.Format(issue, EnUs), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CloseReadinessIssueKind.ClassStillActive)]
    [InlineData(CloseReadinessIssueKind.SubjectWithoutGrades)]
    [InlineData(CloseReadinessIssueKind.BucketWeightsNotHundred)]
    [InlineData(CloseReadinessIssueKind.StudentWithoutGrade)]
    public void Format_NeverContainsIds_ForAnyKind(CloseReadinessIssueKind kind)
    {
        var issue = new CloseReadinessIssue(kind, 4711, "5A", 4711, "Subject A", 4711, "Student", "A", 90m);

        var text = ReadinessItemText.Format(issue, EnUs);

        Assert.DoesNotContain("4711", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_Throws_ForNullIssue()
    {
        Assert.Throws<ArgumentNullException>(() => ReadinessItemText.Format(null!, EnUs));
    }
}
