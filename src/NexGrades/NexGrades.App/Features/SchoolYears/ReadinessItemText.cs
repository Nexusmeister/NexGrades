using System.Globalization;
using System.Text;
using NexGrades.Domain.Services;

namespace NexGrades.App.Features.SchoolYears;

/// <summary>
/// Words a <see cref="CloseReadinessIssue"/> for the user. Each kind has one whole format string, so a
/// translation can reorder the parts. Ids are never part of the text.
/// </summary>
public static class ReadinessItemText
{
    private static readonly CompositeFormat ClassStillActive = CompositeFormat.Parse("Class {0} has not been rolled forward or archived yet.");
    private static readonly CompositeFormat SubjectWithoutGrades = CompositeFormat.Parse("{1} in class {0} has no grades yet.");
    private static readonly CompositeFormat BucketWeightsNotHundred = CompositeFormat.Parse("The grade category weights of {1} in class {0} add up to {2:0.##} instead of 100.");
    private static readonly CompositeFormat StudentWithoutGrade = CompositeFormat.Parse("{2}, {3} has no grade in {1} in class {0}.");

    /// <summary>Formats <paramref name="issue"/> for display with <paramref name="culture"/>.</summary>
    /// <param name="issue">The finding from the close-readiness check.</param>
    /// <param name="culture">The culture for numbers; normally the current UI culture.</param>
    public static string Format(CloseReadinessIssue issue, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(issue);

        return issue.Kind switch
        {
            CloseReadinessIssueKind.ClassStillActive =>
                string.Format(culture, ClassStillActive, issue.ClassName),
            CloseReadinessIssueKind.SubjectWithoutGrades =>
                string.Format(culture, SubjectWithoutGrades, issue.ClassName, issue.SubjectName),
            CloseReadinessIssueKind.BucketWeightsNotHundred =>
                string.Format(culture, BucketWeightsNotHundred, issue.ClassName, issue.SubjectName, issue.WeightTotal),
            CloseReadinessIssueKind.StudentWithoutGrade =>
                string.Format(culture, StudentWithoutGrade, issue.ClassName, issue.SubjectName, issue.StudentLastName, issue.StudentFirstName),
            _ => throw new ArgumentOutOfRangeException(nameof(issue)),
        };
    }
}
