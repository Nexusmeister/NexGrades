using CommunityToolkit.Mvvm.ComponentModel;

namespace NexGrades.App.Features.Students;

/// <summary>
/// Input state of the export-notes dialog: the date range and whether archived notes are included. The Export
/// button is enabled only while both dates are set and the range is not reversed.
/// </summary>
/// <param name="defaultFrom">The initial start of the range.</param>
/// <param name="defaultTo">The initial end of the range.</param>
public sealed partial class ExportNotesDialogViewModel(DateOnly defaultFrom, DateOnly defaultTo) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationMessage), nameof(HasValidationMessage), nameof(IsValid))]
    private DateTime? _from = defaultFrom.ToDateTime(TimeOnly.MinValue);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationMessage), nameof(HasValidationMessage), nameof(IsValid))]
    private DateTime? _to = defaultTo.ToDateTime(TimeOnly.MinValue);

    /// <summary>Whether archived notes are exported too. Off by default.</summary>
    [ObservableProperty]
    private bool _includeArchived;

    /// <summary>Why the input cannot be used yet; <see langword="null"/> when it is valid.</summary>
    public string? ValidationMessage
    {
        get
        {
            if (From is null || To is null)
            {
                return "Enter a start date and an end date.";
            }

            return From > To ? "The start date must not be after the end date." : null;
        }
    }

    /// <summary>True when <see cref="ValidationMessage"/> has something to show.</summary>
    public bool HasValidationMessage => ValidationMessage is not null;

    /// <summary>True when the dialog's input can be exported.</summary>
    public bool IsValid => ValidationMessage is null;

    /// <summary>Returns the choice, or <see langword="null"/> when the input is not valid.</summary>
    public ExportNotesRequest? TryCreateRequest() =>
        From is { } from && To is { } to && IsValid
            ? new ExportNotesRequest(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), IncludeArchived)
            : null;
}
