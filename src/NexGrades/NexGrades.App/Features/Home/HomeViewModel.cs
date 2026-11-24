using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NexGrades.App.Core;
using NexGrades.App.Features.SchoolYears;
using NexGrades.App.Infrastructure;
using NexGrades.App.Localization;
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
    private readonly ILogger<HomeViewModel> _logger;

    // Kept in memory only: "Not now" lasts until the next app start, and the id is never logged.
    private int? _dismissedYearId;
    private int? _reminderYearId;
    private bool _isRefreshing;
    private bool _dismissPending;
    private int _loadVersion;

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
    /// <param name="logger">Records a failed reminder read, without any record content.</param>
    public HomeViewModel(IUserService userService, SchoolYearService schoolYearService, PageNavigator navigator, ILogger<HomeViewModel> logger)
    {
        _schoolYearService = schoolYearService;
        _navigator = navigator;
        _logger = logger;

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
        // Navigation and Loaded both trigger a refresh per visit; the newest run wins and an older one discards its result.
        var version = ++_loadVersion;
        _isRefreshing = true;
        try
        {
            var reminder = await _schoolYearService.GetYearEndReminderAsync();
            if (version != _loadVersion)
            {
                return;
            }

            // A "Not now" pressed while this run was in flight had no year id to record; it applies to this result.
            var dismissedWhileLoading = _dismissPending;
            _dismissPending = false;
            _reminderYearId = reminder?.SchoolYearId;
            if (reminder is not null && dismissedWhileLoading)
            {
                _dismissedYearId = reminder.SchoolYearId;
            }

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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Any failed read (Invariant, SqliteException, InvalidOperationException) hides the card. The page calls
            // this fire-and-forget, so nothing may escape as an unobserved faulted task.
            LogReminderFailed(ex);
            if (version == _loadVersion)
            {
                _dismissPending = false;
                _reminderYearId = null;
                IsYearEndReminderVisible = false;
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                _isRefreshing = false;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading the year-end reminder failed; the reminder card stays hidden.")]
    private partial void LogReminderFailed(Exception exception);

    /// <summary>Opens the School year page and starts the end-of-year flow there.</summary>
    [RelayCommand]
    private void OpenEndOfYear() =>
        _ = _navigator.NavigateWithHierarchy<SchoolYearPage>(SchoolYearPageRequest.StartEndOfYearFlow);

    /// <summary>
    /// Hides the reminder card until the next app start. While a refresh is in flight and no reminder year is known
    /// yet, the dismissal is remembered and applied to that refresh's result, so the card does not reappear.
    /// </summary>
    [RelayCommand]
    private void DismissYearEndReminder()
    {
        if (_reminderYearId is null && _isRefreshing)
        {
            _dismissPending = true;
        }
        else
        {
            _dismissedYearId = _reminderYearId;
        }

        IsYearEndReminderVisible = false;
    }
}
