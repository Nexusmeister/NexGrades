using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using NexGrades.Domain.Services;
using NexGrades.Domain.Time;

namespace NexGrades.App.Core.YearLock;

/// <summary>
/// Input state of the unlock dialog: the reason, the duration and, for a custom end, the date. It validates
/// against the clock each time, because "in the future" changes while the dialog is open.
/// </summary>
/// <param name="schoolYearLabel">The label of the year being unlocked, shown in the title.</param>
/// <param name="clock">Source of "now".</param>
public sealed partial class UnlockYearDialogViewModel(string schoolYearLabel, IClock clock) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationMessage), nameof(HasValidationMessage), nameof(IsValid))]
    private string _reason = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomDate), nameof(ValidationMessage), nameof(HasValidationMessage), nameof(IsValid))]
    private UnlockDurationOption _selectedDuration = UnlockDurationOption.OneHour;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationMessage), nameof(HasValidationMessage), nameof(IsValid))]
    private DateTime? _customDate;

    /// <summary>The dialog title, naming the year being unlocked.</summary>
    public string Title { get; } = string.Format(CultureInfo.CurrentCulture, "Unlock school year {0}", schoolYearLabel);

    /// <summary>The durations on offer, in display order, for a list the teacher picks from.</summary>
    public IReadOnlyList<UnlockDurationChoice> DurationChoices { get; } =
    [
        new(UnlockDurationOption.OneHour, "1 hour"),
        new(UnlockDurationOption.EndOfToday, "Until the end of today"),
        new(UnlockDurationOption.OneWeek, "1 week"),
        new(UnlockDurationOption.CustomDate, "Custom date"),
    ];

    /// <summary>True when the teacher picks the end date; the date picker is only shown then.</summary>
    public bool IsCustomDate => SelectedDuration == UnlockDurationOption.CustomDate;

    /// <summary>The earliest selectable custom date: today.</summary>
    public DateTime CustomDateStart => clock.Now.Date;

    /// <summary>The latest selectable custom date: <see cref="UnlockDuration.MaxCustomDays"/> days from today.</summary>
    public DateTime CustomDateEnd => clock.Now.Date.AddDays(UnlockDuration.MaxCustomDays);

    /// <summary>Why the input cannot be used yet; <see langword="null"/> when it is valid.</summary>
    public string? ValidationMessage
    {
        get
        {
            var trimmed = Reason.Trim();
            if (trimmed.Length == 0)
            {
                return "Enter a reason for unlocking the year.";
            }

            if (trimmed.Length > UnlockService.MaxReasonLength)
            {
                return string.Format(
                    CultureInfo.CurrentCulture, "The reason can be at most {0} characters.", UnlockService.MaxReasonLength);
            }

            var now = clock.Now;
            if (UnlockDuration.Resolve(SelectedDuration, CustomDate, now) is not { } until || until <= now)
            {
                return "Choose an end date that is in the future.";
            }

            if (SelectedDuration == UnlockDurationOption.CustomDate && until.Date > CustomDateEnd)
            {
                return string.Format(
                    CultureInfo.CurrentCulture, "Choose an end date within the next {0} days.", UnlockDuration.MaxCustomDays);
            }

            return null;
        }
    }

    /// <summary>True when <see cref="ValidationMessage"/> has something to show.</summary>
    public bool HasValidationMessage => ValidationMessage is not null;

    /// <summary>True when the input can be submitted.</summary>
    public bool IsValid => ValidationMessage is null;

    /// <summary>
    /// Re-evaluates the validation against the current time, for the dialog to call when the primary button is
    /// pressed: the input may have been valid when typed and not be any more.
    /// </summary>
    /// <returns><see langword="true"/> when the input is valid now.</returns>
    public bool Revalidate()
    {
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(HasValidationMessage));
        OnPropertyChanged(nameof(IsValid));
        return IsValid;
    }

    /// <summary>Builds the result of the dialog from the current input.</summary>
    /// <returns>The end of the unlock and the trimmed reason, or <see langword="null"/> when the input is not valid.</returns>
    public UnlockRequest? TryCreateRequest() =>
        Revalidate() && UnlockDuration.Resolve(SelectedDuration, CustomDate, clock.Now) is { } until
            ? new UnlockRequest(until, Reason.Trim())
            : null;
}
