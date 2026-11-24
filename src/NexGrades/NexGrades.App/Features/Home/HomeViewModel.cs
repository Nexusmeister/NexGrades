using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.App.Features.SchoolYears;
using NexGrades.App.Infrastructure;
using NexGrades.App.Localization;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;

namespace NexGrades.App.Features.Home;

/// <summary>
/// View model of the home page. The greeting is computed once, when the view model is created, from the local hour.
/// Near the end of the Active school year it also offers a reminder card that opens the end-of-year flow.
/// </summary>
public sealed partial class HomeViewModel : ViewModel
{
    private readonly SchoolYearService _schoolYearService;
    private readonly PageNavigator _navigator;

    // Kept in memory only: "Not now" lasts until the next app start, and the id is never logged.
    private int? _dismissedYearId;
    private int? _reminderYearId;

    /// <summary>The greeting shown on the home page: "Good Morning" before noon, "Hello" from noon on.</summary>
    public string WelcomeMessage { get; set; } = TextResources.Text("Home_Welcome_Default");

    /// <summary>Whether the year-end reminder card is shown.</summary>
    [ObservableProperty]
    private bool _isYearEndReminderVisible;

    /// <summary>The sentence on the reminder card: when the Active school year ends, or that it has ended and is not closed.</summary>
    [ObservableProperty]
    private string _yearEndReminderMessage = string.Empty;

    /// <summary>Creates the view model and sets the greeting for the current user.</summary>
    /// <param name="userService">Provides the user name for the greeting.</param>
    /// <param name="schoolYearService">Tells whether the Active school year is near its end.</param>
    /// <param name="navigator">Opens the School year page from the reminder card.</param>
    public HomeViewModel(IUserService userService, SchoolYearService schoolYearService, PageNavigator navigator)
    {
        _schoolYearService = schoolYearService;
        _navigator = navigator;

        var now = DateTime.Now;
        var username = userService.GetUserName();
        WelcomeMessage = TextResources.Format(now.Hour < 12 ? "Home_Welcome_Morning_Format" : "Home_Welcome_Day_Format", username);
    }

    /// <summary>Checks the reminder on every visit, because the date, or the Active year, may have changed since.</summary>
    protected override async Task OnNavigatedToAsync(object? parameter)
    {
        await base.OnNavigatedToAsync(parameter);
        await RefreshYearEndReminderAsync();
    }

    /// <summary>
    /// Asks the Domain whether the reminder is due and shows or hides the card. A year the teacher dismissed stays
    /// hidden until the next app start. A failed read hides the card without a message: it is a convenience, not
    /// something the teacher asked for.
    /// </summary>
    public async Task RefreshYearEndReminderAsync()
    {
        try
        {
            var reminder = await _schoolYearService.GetYearEndReminderAsync();
            _reminderYearId = reminder?.SchoolYearId;
            if (reminder is null || reminder.SchoolYearId == _dismissedYearId)
            {
                IsYearEndReminderVisible = false;
                return;
            }

            YearEndReminderMessage = reminder.HasEnded
                ? TextResources.Format("Home_YearEnd_Ended_Format", reminder.Label, reminder.EndDate)
                : TextResources.Format("Home_YearEnd_Upcoming_Format", reminder.Label, reminder.EndDate);
            IsYearEndReminderVisible = true;
        }
        catch (Exception ex) when (ex is InvariantViolationException or DbUpdateException)
        {
            _reminderYearId = null;
            IsYearEndReminderVisible = false;
        }
    }

    /// <summary>Opens the School year page and starts the end-of-year flow there.</summary>
    [RelayCommand]
    private void OpenEndOfYear() =>
        _ = _navigator.NavigateWithHierarchy<SchoolYearPage>(SchoolYearPageRequest.StartEndOfYearFlow);

    /// <summary>Hides the reminder card until the next app start.</summary>
    [RelayCommand]
    private void DismissYearEndReminder()
    {
        _dismissedYearId = _reminderYearId;
        IsYearEndReminderVisible = false;
    }
}
