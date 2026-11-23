using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using NexGrades.App.Localization;

namespace NexGrades.App.Features.Grades;

/// <summary>
/// The final (certificate) grade cell of one student. <see cref="Text"/> holds the teacher's override as a whole
/// grade ("3"), or is empty when the proposal is used. Like <see cref="GradeCellViewModel"/> it tells its owner when
/// the grid commits an edit, and keeps text that is not legal or could not be saved together with
/// <see cref="HasError"/> instead of throwing it away.
/// </summary>
public sealed partial class FinalGradeCellViewModel : ObservableObject, IGradeSheetEditableCell
{
    private readonly Action<FinalGradeCellViewModel> _textCommitted;
    private bool _isWritable;
    private bool _suppressCommit;

    /// <summary>Creates the cell from the loaded sheet.</summary>
    /// <param name="studentId">The student the cell belongs to.</param>
    /// <param name="isExempt">Whether the student is exempt from the subject.</param>
    /// <param name="finalGradeOverride">The stored override, or null when the proposal is used.</param>
    /// <param name="proposal">The proposed final grade, or null when there is no average.</param>
    /// <param name="isWritable">Whether the school year accepts writes right now.</param>
    /// <param name="textCommitted">Called when the grid commits a changed text; the owner saves it.</param>
    public FinalGradeCellViewModel(
        int studentId,
        bool isExempt,
        decimal? finalGradeOverride,
        decimal? proposal,
        bool isWritable,
        Action<FinalGradeCellViewModel> textCommitted)
    {
        ArgumentNullException.ThrowIfNull(textCommitted);

        StudentId = studentId;
        IsExempt = isExempt;
        Override = finalGradeOverride;
        Proposal = proposal;
        _isWritable = isWritable;
        _textCommitted = textCommitted;
        _text = FormatGrade(finalGradeOverride);
    }

    /// <summary>The student the cell belongs to.</summary>
    public int StudentId { get; }

    /// <summary>True when the student is exempt from the subject; the cell is read-only.</summary>
    public bool IsExempt { get; }

    /// <summary>The saved override, or null when the proposal is used.</summary>
    public decimal? Override { get; private set; }

    /// <summary>The proposed final grade from the current average, or null when there is none.</summary>
    public decimal? Proposal { get; private set; }

    /// <summary>The override as the user sees and types it; empty for no override.</summary>
    [ObservableProperty]
    private string _text;

    /// <summary>True when the text is not a legal final grade or could not be saved; the cell keeps the text and the mark.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisualState))]
    [NotifyPropertyChangedFor(nameof(Description))]
    private bool _hasError;

    /// <summary>True when the cell holds an override.</summary>
    public bool HasOverride => Override is not null;

    /// <summary>True when the user may type into the cell now: the student is not exempt and the year is writable.</summary>
    public bool IsEditable => !IsExempt && _isWritable;

    /// <summary>What the cell looks like, as one value so the page can style it with plain triggers.</summary>
    public GradeCellVisualState VisualState =>
        HasError ? GradeCellVisualState.Error
        : IsExempt ? GradeCellVisualState.Exempt
        : HasOverride ? GradeCellVisualState.Override
        : GradeCellVisualState.Normal;

    /// <summary>A short whole-sentence explanation of the cell's state for a tooltip or screen reader.</summary>
    public string Description => VisualState switch
    {
        GradeCellVisualState.Error => TextResources.Text("Grades_FinalCell_Error_Description"),
        GradeCellVisualState.Exempt => TextResources.Text("Grades_Cell_Exempt_Description"),
        GradeCellVisualState.Override => Proposal is { } proposal
            ? TextResources.Format("Grades_FinalCell_Override_Description_Format", FormatGrade(proposal))
            : TextResources.Text("Grades_FinalCell_OverrideNoProposal_Description"),
        _ => TextResources.Text("Grades_FinalCell_NoOverride_Description"),
    };

    /// <summary>True while a save of this cell runs; the owner serializes saves per cell with it.</summary>
    internal bool IsSaving { get; set; }

    /// <summary>True when the text was committed again while a save ran; the owner saves the latest text afterwards.</summary>
    internal bool SaveQueued { get; set; }

    /// <summary>
    /// True when the cell holds input that is not saved yet: a save runs or is queued, or the trimmed text differs
    /// from the saved override. The leave guard uses it so typed input is never lost silently.
    /// </summary>
    internal bool HasUnsavedText =>
        IsSaving || SaveQueued || !string.Equals(Text.Trim(), FormatGrade(Override), StringComparison.Ordinal);

    bool IGradeSheetEditableCell.IsSaving { get => IsSaving; set => IsSaving = value; }

    bool IGradeSheetEditableCell.SaveQueued { get => SaveQueued; set => SaveQueued = value; }

    bool IGradeSheetEditableCell.HasUnsavedText => HasUnsavedText;

    void IGradeSheetEditableCell.RetrySaveIfFailed() => RetrySaveIfFailed();

    /// <summary>Formats a final grade as a culture-neutral whole number such as "3"; empty for null.</summary>
    /// <param name="grade">The grade, or null.</param>
    /// <returns>The token.</returns>
    internal static string FormatGrade(decimal? grade) =>
        grade is { } value ? decimal.Truncate(value).ToString("0", CultureInfo.InvariantCulture) : string.Empty;

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

    /// <summary>Updates the proposal after the row's average changed.</summary>
    /// <param name="proposal">The proposed final grade, or null when there is none.</param>
    internal void SetProposal(decimal? proposal)
    {
        if (Proposal == proposal)
        {
            return;
        }

        Proposal = proposal;
        OnPropertyChanged(nameof(Proposal));
        OnPropertyChanged(nameof(Description));
    }

    /// <summary>
    /// Shows <paramref name="value"/> as the saved override of a save that started with <paramref name="savedText"/>.
    /// The override and its derived flags are always updated. The text and the error mark are only replaced when the
    /// user has not committed newer text since the save started; otherwise that newer text stays for the queued save.
    /// </summary>
    /// <param name="value">The saved override, or null when it was cleared.</param>
    /// <param name="savedText">The trimmed text the save started with, or null to always replace the text.</param>
    internal void ApplyOverride(decimal? value, string? savedText)
    {
        Override = value;
        if (savedText is null || string.Equals(Text.Trim(), savedText, StringComparison.Ordinal))
        {
            SetTextWithoutCommit(FormatGrade(value));
            HasError = false;
        }

        OnPropertyChanged(nameof(Override));
        OnPropertyChanged(nameof(HasOverride));
        OnPropertyChanged(nameof(VisualState));
        OnPropertyChanged(nameof(Description));
    }

    /// <summary>
    /// Saves the cell again when an edit ends without a changed text, but only if the last save failed or the text
    /// was not legal. Without this, leaving such a cell untouched could never retry it.
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
