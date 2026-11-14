using CommunityToolkit.Mvvm.ComponentModel;

namespace NexGrades.App.Features.Grades;

/// <summary>
/// The per-bucket values of one student row: the bucket average and the grades that belong to no assessment.
/// Observable so a saved cell updates its row's averages live.
/// </summary>
public sealed partial class GradeSheetBucketCell : ObservableObject
{
    /// <summary>The bucket average formatted for display; an en dash when there is none.</summary>
    [ObservableProperty]
    private string _averageText = GradeAverageFormatter.Missing;

    /// <summary>The tokens of the bucket's grades that have no assessment, joined with the current culture's list separator. A voided one is in parentheses; empty when there are none.</summary>
    [ObservableProperty]
    private string _otherGradesText = string.Empty;
}
