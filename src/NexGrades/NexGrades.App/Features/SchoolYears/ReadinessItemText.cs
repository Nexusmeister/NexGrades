using System.Globalization;
using NexGrades.App.Localization;
using NexGrades.Domain.Services;

namespace NexGrades.App.Features.SchoolYears;

/// <summary>
/// Words a <see cref="CloseReadinessIssue"/> for the user. Each kind has one whole format string, so a
/// translation can reorder the parts. Ids are never part of the text.
/// </summary>
public static class ReadinessItemText
{
    /// <summary>Formats <paramref name="issue"/> for display with <paramref name="culture"/>.</summary>
    /// <param name="issue">The finding from the close-readiness check.</param>
    /// <param name="culture">The culture for the wording and the numbers; normally the current UI culture.</param>
    public static string Format(CloseReadinessIssue issue, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(issue);

        return issue.Kind switch
        {
            CloseReadinessIssueKind.ClassStillActive =>
                string.Format(culture, TextResources.Get("SchoolYears_Readiness_ClassStillActive_Format", culture), issue.ClassName),
            CloseReadinessIssueKind.SubjectWithoutGrades =>
                string.Format(culture, TextResources.Get("SchoolYears_Readiness_SubjectWithoutGrades_Format", culture), issue.ClassName, issue.SubjectName),
            CloseReadinessIssueKind.BucketWeightsNotHundred =>
                string.Format(culture, TextResources.Get("SchoolYears_Readiness_WeightsNotHundred_Format", culture), issue.ClassName, issue.SubjectName, issue.WeightTotal),
            CloseReadinessIssueKind.StudentWithoutGrade =>
                string.Format(culture, TextResources.Get("SchoolYears_Readiness_StudentWithoutGrade_Format", culture), issue.ClassName, issue.SubjectName, issue.StudentLastName, issue.StudentFirstName),
            _ => throw new ArgumentOutOfRangeException(nameof(issue)),
        };
    }
}
