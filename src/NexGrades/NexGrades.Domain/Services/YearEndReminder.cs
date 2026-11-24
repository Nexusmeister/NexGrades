namespace NexGrades.Domain.Services;

/// <summary>
/// Tells the Home page that the Active school year is about to end, or has ended without being closed. The Domain
/// decides when the reminder is due (<see cref="SchoolYearService.IsYearEndReminderDue"/>), so the view only words it.
/// </summary>
/// <param name="SchoolYearId">The Active school year the reminder is about; the page uses it to remember a dismissal.</param>
/// <param name="Label">The display label of the year, e.g. "2025/26".</param>
/// <param name="EndDate">Last day of the school year.</param>
/// <param name="HasEnded">
/// Whether the end date has already passed. The year stays Active until it is closed, so the reminder goes on and
/// the page words it in the past tense.
/// </param>
public sealed record YearEndReminder(int SchoolYearId, string Label, DateTime EndDate, bool HasEnded);
