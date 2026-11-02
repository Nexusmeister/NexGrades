using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;

namespace NexGrades.App.Features.SchoolYears;

/// <summary>
/// One class in the classes step: what the teacher chose so far and, once the class is done, how it ended.
/// The row outlives reloads, so an unsaved choice or name survives a visit to another page.
/// </summary>
public sealed partial class ClassChoiceRow : ObservableObject
{
    private static readonly CompositeFormat _carryOverFormat = CompositeFormat.Parse("{0} students carry over.");
    private static readonly CompositeFormat _rolledForwardFormat = CompositeFormat.Parse("Rolled forward as {0}.");
    private const string ArchivedFormat = "Archived.";
    private const string FailedFormat = "This class could not be rolled forward or archived. Check the new name, and that none of its students is already in a class of the next year.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRollForwardChosen), nameof(IsArchiveChosen), nameof(IsRollForwardPending))]
    private ClassYearEndChoice _choice;

    [ObservableProperty]
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
    public string CarryOverText => string.Format(CultureInfo.CurrentCulture, _carryOverFormat, CarryOverStudentCount);

    /// <summary>How a finished class ended, or empty while it is still pending.</summary>
    public string DoneText => Status switch
    {
        ClassInstanceStatus.RolledForward => string.Format(CultureInfo.CurrentCulture, _rolledForwardFormat, SuccessorName),
        ClassInstanceStatus.Archived => ArchivedFormat,
        _ => string.Empty,
    };

    /// <summary>Explains a failed attempt for this class, without any technical detail.</summary>
    public string FailedText { get; } = FailedFormat;

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
