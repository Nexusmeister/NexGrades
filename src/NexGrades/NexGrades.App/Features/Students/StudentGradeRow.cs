using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using NexGrades.Domain.Grading;

namespace NexGrades.App.Features.Students;

/// <summary>
/// One grade in the student's grade list. It is observable only so a void or restore shows at once; the list
/// is reloaded afterwards so the averages catch up.
/// </summary>
public sealed class StudentGradeRow : ObservableObject
{
    private bool _isOwnVoided;
    private string? _voidedText;

    /// <summary>Creates the row from the loaded grade.</summary>
    /// <param name="grade">The grade.</param>
    /// <param name="isExempt">Whether the student is exempt from the grade's subject.</param>
    public StudentGradeRow(StudentGradeEntry grade, bool isExempt)
    {
        ArgumentNullException.ThrowIfNull(grade);

        Id = grade.Id;
        BucketId = grade.GradeBucketId;
        AssessmentId = grade.AssessmentId;
        AssessmentTitle = grade.AssessmentTitle;
        Token = grade.Token;
        RecordedOn = grade.RecordedOn;
        DateText = grade.RecordedOn.ToString("d", CultureInfo.CurrentCulture);
        Label = grade.Label;
        Comment = grade.Comment;
        IsExempt = isExempt;
        IsAssessmentVoided = grade.IsAssessmentVoided;

        _isOwnVoided = grade.VoidedOn is not null;
        _voidedText = grade.VoidedOn is { } voidedOn ? FormatVoided(voidedOn) : null;
    }

    /// <summary>The grade id.</summary>
    public int Id { get; }

    /// <summary>The bucket the grade belongs to.</summary>
    public int BucketId { get; }

    /// <summary>The assessment the grade belongs to, or <see langword="null"/> for a single grade.</summary>
    public int? AssessmentId { get; }

    /// <summary>The assessment title, or <see langword="null"/> for a single grade.</summary>
    public string? AssessmentTitle { get; }

    /// <summary>The grade token, for example "2-"; culture-neutral.</summary>
    public string Token { get; }

    /// <summary>The date the grade applies to.</summary>
    public DateOnly RecordedOn { get; }

    /// <summary>The date formatted for the current culture.</summary>
    public string DateText { get; }

    /// <summary>The optional label.</summary>
    public string? Label { get; }

    /// <summary>The optional comment.</summary>
    public string? Comment { get; }

    /// <summary>Whether the student is exempt from the subject: such a grade can be voided but not edited or restored.</summary>
    public bool IsExempt { get; }

    /// <summary>Whether the grade's assessment is voided, which excludes the grade from the averages.</summary>
    public bool IsAssessmentVoided { get; }

    /// <summary>True when the grade is excluded from the averages, because it or its assessment is voided.</summary>
    public bool IsVoided => _isOwnVoided || IsAssessmentVoided;

    /// <summary>Tells when the grade was voided, or that its assessment is; <see langword="null"/> while it counts.</summary>
    public string? VoidedText => _isOwnVoided ? _voidedText : IsAssessmentVoided ? "The assessment is voided." : null;

    /// <summary>True when the grade can be edited: it is not voided and the student is not exempt.</summary>
    public bool CanEdit => !IsVoided && !IsExempt;

    /// <summary>True when the grade can be voided: it still counts.</summary>
    public bool CanVoid => !IsVoided;

    /// <summary>True when the grade can be restored: it was voided itself (not only through its assessment) and the student is not exempt.</summary>
    public bool CanRestore => _isOwnVoided && !IsExempt;

    /// <summary>Marks the grade as voided right after a successful void.</summary>
    /// <param name="voidedOn">When it was voided.</param>
    public void MarkVoided(DateTime voidedOn)
    {
        _isOwnVoided = true;
        _voidedText = FormatVoided(voidedOn);
        RaiseStateChanged();
    }

    /// <summary>Marks the grade as counting again right after a successful restore.</summary>
    public void MarkRestored()
    {
        _isOwnVoided = false;
        _voidedText = null;
        RaiseStateChanged();
    }

    private static string FormatVoided(DateTime voidedOn) =>
        string.Format(CultureInfo.CurrentCulture, "Voided {0:g}", voidedOn);

    private void RaiseStateChanged()
    {
        OnPropertyChanged(nameof(IsVoided));
        OnPropertyChanged(nameof(VoidedText));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanVoid));
        OnPropertyChanged(nameof(CanRestore));
    }
}
