using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using NexGrades.App.Localization;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;

namespace NexGrades.App.Features.SchoolYears;

/// <summary>
/// One class in the classes step: what the teacher chose so far and, once the class is done, how it ended.
/// The row outlives reloads, so an unsaved choice or name survives a visit to another page.
/// </summary>
public sealed partial class ClassChoiceRow : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRollForwardChosen), nameof(IsArchiveChosen), nameof(IsRollForwardPending))]
    private ClassYearEndChoice _choice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNewNameValid), nameof(NewNameValidationMessage), nameof(HasNewNameValidationMessage))]
    private string _newName = string.Empty;

    [ObservableProperty]
    private bool _endClassGroup;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDone), nameof(IsPending), nameof(IsRollForwardPending), nameof(DoneText))]
    private ClassInstanceStatus _status;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CarryOverText))]
    private int _carryOverStudentCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DoneText))]
    private string? _successorName;

    [ObservableProperty]
    private bool _hasFailed;

    /// <summary>Creates a row from the state of a class; the name box starts with the suggested name.</summary>
    /// <param name="state">The class state to show.</param>
    public ClassChoiceRow(ClassRolloverState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        ClassInstanceId = state.ClassInstanceId;
        Name = state.Name;
        NewName = state.SuggestedName;
        Apply(state);
    }

    /// <summary>The class instance id, for the Domain calls only.</summary>
    public int ClassInstanceId { get; }

    /// <summary>
    /// A radio group name unique to this row, so two classes with the same name never share one group.
    /// </summary>
    public string GroupName => string.Create(CultureInfo.InvariantCulture, $"class-choice-{ClassInstanceId}");

    /// <summary>The class name in the closing year.</summary>
    public string Name { get; }

    /// <summary>The longest new class name the Domain accepts; the name box stops typing at this length.</summary>
    public int MaxNameLength { get; } = ClassService.MaxNameLength;

    /// <summary>
    /// Whether <see cref="NewName"/> is acceptable: not empty, and within <see cref="MaxNameLength"/> unless it is
    /// the unchanged old name (the Domain keeps such a legacy name valid).
    /// </summary>
    public bool IsNewNameValid
    {
        get
        {
            var trimmed = NewName.Trim();
            return trimmed.Length > 0 && (trimmed.Length <= MaxNameLength || trimmed == Name);
        }
    }

    /// <summary>
    /// Why <see cref="NewName"/> is refused: it is longer than <see cref="MaxNameLength"/> and is not the unchanged
    /// old name. Null when there is nothing to explain; an empty name keeps no message.
    /// </summary>
    public string? NewNameValidationMessage
    {
        get
        {
            var trimmed = NewName.Trim();
            return trimmed.Length > MaxNameLength && trimmed != Name
                ? TextResources.Format("SchoolYears_Class_NewName_TooLong_Format", MaxNameLength)
                : null;
        }
    }

    /// <summary>Whether <see cref="NewNameValidationMessage"/> has a message to show.</summary>
    public bool HasNewNameValidationMessage => NewNameValidationMessage is not null;

    /// <summary>Whether the class has been rolled forward or archived already.</summary>
    public bool IsDone => Status != ClassInstanceStatus.Active;

    /// <summary>Whether the class still needs a decision.</summary>
    public bool IsPending => !IsDone;

    /// <summary>Whether the class is waiting and is to be rolled forward, so the new-name box is shown.</summary>
    public bool IsRollForwardPending => IsPending && Choice == ClassYearEndChoice.RollForward;

    /// <summary>Two-way helper for a radio button: roll the class forward.</summary>
    public bool IsRollForwardChosen
    {
        get => Choice == ClassYearEndChoice.RollForward;
        set
        {
            if (value)
            {
                Choice = ClassYearEndChoice.RollForward;
            }
        }
    }

    /// <summary>Two-way helper for a radio button: archive the class.</summary>
    public bool IsArchiveChosen
    {
        get => Choice == ClassYearEndChoice.Archive;
        set
        {
            if (value)
            {
                Choice = ClassYearEndChoice.Archive;
            }
        }
    }

    /// <summary>How many students carry over, as a sentence for the current UI culture.</summary>
    public string CarryOverText => TextResources.Format(
        CarryOverStudentCount == 1 ? "SchoolYears_Class_CarryOver_One_Format" : "SchoolYears_Class_CarryOver_Many_Format",
        CarryOverStudentCount);

    /// <summary>How a finished class ended, or empty while it is still pending.</summary>
    public string DoneText => Status switch
    {
        ClassInstanceStatus.RolledForward => TextResources.Format("SchoolYears_Class_RolledForward_Format", SuccessorName),
        ClassInstanceStatus.Archived => TextResources.Text("SchoolYears_Class_Archived"),
        _ => string.Empty,
    };

    /// <summary>Explains a failed attempt for this class, without any technical detail.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Bound from XAML, which needs an instance property.")]
    public string FailedText => TextResources.Text("SchoolYears_Class_Failed");

    /// <summary>Refreshes the stored facts from <paramref name="state"/> and keeps the unsaved choice and name.</summary>
    /// <param name="state">The freshly loaded state of the same class.</param>
    public void Apply(ClassRolloverState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        Status = state.Status;
        CarryOverStudentCount = state.CarryOverStudentCount;
        SuccessorName = state.SuccessorName;
    }
}
