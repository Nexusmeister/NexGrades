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
    /// <summary>Formats <paramref name="issue"/> for display.</summary>
    /// <param name="issue">The finding from the close-readiness check.</param>
    /// <param name="textCulture">The culture for the wording; normally the current UI culture.</param>
    /// <param name="formatCulture">The culture for the numbers; normally the current (regional format) culture.</param>
    public static string Format(CloseReadinessIssue issue, CultureInfo textCulture, CultureInfo formatCulture)
    {
        ArgumentNullException.ThrowIfNull(issue);

        return issue.Kind switch
        {
            CloseReadinessIssueKind.ClassStillActive =>
                string.Format(formatCulture, TextResources.Get("SchoolYears_Readiness_ClassStillActive_Format", textCulture), issue.ClassName),
            CloseReadinessIssueKind.SubjectWithoutGrades =>
                string.Format(formatCulture, TextResources.Get("SchoolYears_Readiness_SubjectWithoutGrades_Format", textCulture), issue.ClassName, issue.SubjectName),
            CloseReadinessIssueKind.BucketWeightsNotHundred =>
                string.Format(formatCulture, TextResources.Get("SchoolYears_Readiness_WeightsNotHundred_Format", textCulture), issue.ClassName, issue.SubjectName, issue.WeightTotal),
            CloseReadinessIssueKind.StudentWithoutGrade =>
                string.Format(formatCulture, TextResources.Get("SchoolYears_Readiness_StudentWithoutGrade_Format", textCulture), issue.ClassName, issue.SubjectName, issue.StudentLastName, issue.StudentFirstName),
            _ => throw new ArgumentOutOfRangeException(nameof(issue)),
        };
    }
}
