using CommunityToolkit.Mvvm.ComponentModel;
using NexGrades.App.Localization;
using NexGrades.Domain.Grading;

namespace NexGrades.App.Features.Grades;

/// <summary>
/// One grade cell: a student in one assessment. <see cref="Text"/> is what the user types; when the grid commits
/// an edit the owner is told, so each cell saves by itself. A typed value that is not on the scale, or that could
/// not be saved, stays in the cell with <see cref="HasError"/> set instead of being thrown away.
/// </summary>
public sealed partial class GradeCellViewModel : ObservableObject, IGradeSheetEditableCell
{
    private readonly Action<GradeCellViewModel> _textCommitted;
    private bool _isWritable;
    private bool _suppressCommit;

    /// <summary>Creates the cell from the loaded sheet.</summary>
    /// <param name="studentId">The student the cell belongs to.</param>
    /// <param name="cell">The loaded cell.</param>
    /// <param name="assessmentVoided">Whether the cell's assessment is voided.</param>
    /// <param name="isWritable">Whether the school year accepts writes right now.</param>
    /// <param name="textCommitted">Called when the grid commits a changed text; the owner saves it.</param>
    public GradeCellViewModel(
        int studentId, GradeSheetCell cell, bool assessmentVoided, bool isWritable, Action<GradeCellViewModel> textCommitted)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(textCommitted);

        StudentId = studentId;
        AssessmentId = cell.AssessmentId;
        State = cell.State;
        IsAssessmentVoided = assessmentVoided;
        _isWritable = isWritable;
        _textCommitted = textCommitted;
        Grade = cell.Grade;
        _text = cell.Grade?.Token ?? string.Empty;
    }

    /// <summary>The student the cell belongs to.</summary>
    public int StudentId { get; }

    /// <summary>The assessment (column) the cell belongs to.</summary>
    public int AssessmentId { get; }

    /// <summary>Whether the student can be graded on the assessment's date.</summary>
    public GradeSheetCellState State { get; }

    /// <summary>True when the cell's assessment is voided; its cells are read-only.</summary>
    public bool IsAssessmentVoided { get; }

    /// <summary>The grade shown in the cell (the active one, else the latest voided one), or null when empty.</summary>
    public GradeSheetGrade? Grade { get; private set; }

    /// <summary>The grade token as the user sees and types it; empty for no grade.</summary>
    [ObservableProperty]
    private string _text;

    /// <summary>True when the text is not a legal token or could not be saved; the cell keeps the text and the mark.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisualState))]
    [NotifyPropertyChangedFor(nameof(Description))]
    private bool _hasError;

    /// <summary>True when the cell holds a grade that counts: not voided, in an assessment that is not voided.</summary>
    public bool HasActiveGrade => Grade is { VoidedOn: null } && !IsAssessmentVoided;

    /// <summary>True when the cell shows a voided grade that can be restored on its own.</summary>
    public bool IsVoidedGrade => Grade is { VoidedOn: not null } && !IsAssessmentVoided;

    /// <summary>True when the student was not enrolled on the assessment's date.</summary>
    public bool IsNotEnrolled => State == GradeSheetCellState.NotEnrolled;

    /// <summary>True when the student is exempt from the subject.</summary>
    public bool IsExempt => State == GradeSheetCellState.Exempt;

    /// <summary>True when the user may type into the cell now: gradable, nothing voided, and the year is writable.</summary>
    public bool IsEditable =>
        State == GradeSheetCellState.Editable && !IsAssessmentVoided && !IsVoidedGrade && _isWritable;

    /// <summary>
    /// What the cell looks like, as one value so the page can style it with plain triggers. The mark must not rely
    /// on colour alone (add text or an icon), so <see cref="Description"/> explains it too.
    /// </summary>
    public GradeCellVisualState VisualState =>
        HasError ? GradeCellVisualState.Error
        : IsExempt ? GradeCellVisualState.Exempt
        : IsNotEnrolled ? GradeCellVisualState.NotEnrolled
        : IsAssessmentVoided ? GradeCellVisualState.AssessmentVoided
        : IsVoidedGrade ? GradeCellVisualState.Voided
        : GradeCellVisualState.Normal;

    /// <summary>
    /// A short whole-sentence explanation of the cell's state for a tooltip or screen reader. Empty for a
    /// normal cell.
    /// </summary>
    public string Description => VisualState switch
    {
        GradeCellVisualState.Error => TextResources.Text("Grades_Cell_Error_Description"),
        GradeCellVisualState.Exempt => TextResources.Text("Grades_Cell_Exempt_Description"),
        GradeCellVisualState.NotEnrolled => TextResources.Text("Grades_Cell_NotEnrolled_Description"),
        GradeCellVisualState.AssessmentVoided => TextResources.Text("Grades_Cell_AssessmentVoided_Description"),
        GradeCellVisualState.Voided => TextResources.Text("Grades_Cell_Voided_Description"),
        _ => string.Empty,
    };

    /// <summary>True while a save of this cell runs; the owner serializes saves per cell with it.</summary>
    internal bool IsSaving { get; set; }

    /// <summary>True when the text was committed again while a save ran; the owner saves the latest text afterwards.</summary>
    internal bool SaveQueued { get; set; }

    bool IGradeSheetEditableCell.IsSaving { get => IsSaving; set => IsSaving = value; }

    bool IGradeSheetEditableCell.SaveQueued { get => SaveQueued; set => SaveQueued = value; }

    bool IGradeSheetEditableCell.HasUnsavedText => HasUnsavedText;

    void IGradeSheetEditableCell.RetrySaveIfFailed() => RetrySaveIfFailed();

    /// <summary>The token of the saved active grade, or empty when there is none.</summary>
    internal string SavedToken => HasActiveGrade ? Grade!.Token : string.Empty;

    /// <summary>Updates whether the year accepts writes; keeps whatever the user typed.</summary>
    /// <param name="isWritable">The year's current writability.</param>
    internal void SetWritable(bool isWritable)
    {
        if (_isWritable == isWritable)
        {
            return;
        }

        _isWritable = isWritable;
        OnPropertyChanged(nameof(IsEditable));
    }

    /// <summary>
    /// True when the cell holds input that is not saved yet: a save runs or is queued, or the trimmed text differs
    /// from the shown grade's token. The leave guard uses it so a value typed during a save is never lost silently.
    /// </summary>
    internal bool HasUnsavedText =>
        IsSaving || SaveQueued || !string.Equals(Text.Trim(), Grade?.Token ?? string.Empty, StringComparison.Ordinal);

    /// <summary>Shows <paramref name="grade"/> as the saved state of the cell and clears any error.</summary>
    /// <param name="grade">The saved grade, or null when the cell is now empty.</param>
    internal void ApplyGrade(GradeSheetGrade? grade) => ApplyGrade(grade, null);

    /// <summary>
    /// Shows <paramref name="grade"/> as the saved state of a save that started with <paramref name="savedText"/>.
    /// The grade and its derived flags are always updated. The text and the error mark are only replaced when the
    /// user has not committed newer text since the save started; otherwise that newer text stays for the queued save.
    /// </summary>
    /// <param name="grade">The saved grade, or null when the cell is now empty.</param>
    /// <param name="savedText">The trimmed text the save started with, or null to always replace the text.</param>
    internal void ApplyGrade(GradeSheetGrade? grade, string? savedText)
    {
        Grade = grade;
        if (savedText is null || string.Equals(Text.Trim(), savedText, StringComparison.Ordinal))
        {
            SetTextWithoutCommit(grade?.Token ?? string.Empty);
            HasError = false;
        }

        OnPropertyChanged(nameof(Grade));
        OnPropertyChanged(nameof(HasActiveGrade));
        OnPropertyChanged(nameof(IsVoidedGrade));
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(VisualState));
        OnPropertyChanged(nameof(Description));
    }

    /// <summary>
    /// Saves the cell again when an edit ends without a changed text, but only if the last save failed or the text
    /// was not a legal token. Without this, leaving such a cell untouched could never retry it.
    /// </summary>
    internal void RetrySaveIfFailed()
    {
        if (HasError)
        {
            _textCommitted(this);
        }
    }

    /// <summary>Replaces the text (for example with the canonical token) without triggering a save.</summary>
    /// <param name="text">The new text.</param>
    internal void SetTextWithoutCommit(string text)
    {
        _suppressCommit = true;
        try
        {
            Text = text;
        }
        finally
        {
            _suppressCommit = false;
        }
    }

    partial void OnTextChanged(string value)
    {
        if (!_suppressCommit)
        {
            _textCommitted(this);
        }
    }
}
