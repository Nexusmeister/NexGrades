using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Time;

namespace NexGrades.Domain.Services;

/// <summary>
/// School-year lifecycle rules: the startup bootstrap of an Active year, and the lookups and creation of the
/// Planned successor year that the guided "End school year" flow (#72) needs, and editing that Planned year until
/// it becomes active. Closing and activating lives in <see cref="YearCloseService"/>.
/// </summary>
/// <param name="contextFactory">Creates the short-lived context that each call opens.</param>
/// <param name="clock">Source of "now", used to pick the school year that contains today.</param>
/// <param name="logger">Receives a content-free line when a year is created.</param>
/// <remarks>Opens a short-lived context per call; see <see cref="EnrollmentService"/> for why.</remarks>
public sealed partial class SchoolYearService(IDbContextFactory<AppDbContext> contextFactory, IClock clock, ILogger<SchoolYearService> logger)
{
    /// <summary>
    /// Creates an Active school year only when none exists, so Students, Classes and Subjects have a year to work
    /// in at startup. It does nothing when a year is already Active. Planned or Closed years do not count, so an
    /// Active year is created even when only those exist. The year is the one that contains today (August to July).
    /// Later years are not created here: <see cref="CreateNextYearAsync"/> adds the Planned successor of the Active
    /// year, and the School year page calls it.
    /// </summary>
    public Task EnsureActiveYearAsync(CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("SchoolYearService.EnsureActiveYear", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var hasActiveYear = await context.SchoolYears.AsNoTracking().AnyAsync(sy => sy.Status == SchoolYearStatus.Active, cancellationToken);
            if (hasActiveYear)
            {
                return;
            }

            var today = clock.Now;
            // German school years run August -> July; if we're before August, the current year started last August.
            var startYear = today.Month >= 8 ? today.Year : today.Year - 1;

            context.SchoolYears.Add(new SchoolYear
            {
                Label = FormatLabel(startYear),
                StartDate = new DateTime(startYear, 8, 1),
                EndDate = new DateTime(startYear + 1, 7, 31),
                Status = SchoolYearStatus.Active,
            });

            await context.SaveChangesAsync(cancellationToken);
            LogActiveYearCreated();
        });

    /// <summary>Returns the Active school year, or <see langword="null"/> when there is none.</summary>
    public Task<SchoolYearSummary?> GetActiveYearAsync(CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("SchoolYearService.GetActiveYear", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.SchoolYears.AsNoTracking()
                .Where(sy => sy.Status == SchoolYearStatus.Active)
                .Select(sy => new SchoolYearSummary(sy.Id, sy.Label, sy.StartDate, sy.EndDate, sy.Status))
                .FirstOrDefaultAsync(cancellationToken);
        });

    /// <summary>How many days before the Active year's end date the Home page starts reminding the teacher.</summary>
    public const int YearEndReminderLeadDays = 28;

    /// <summary>
    /// Whether the end-of-year reminder is due: from <see cref="YearEndReminderLeadDays"/> days before
    /// <paramref name="endDate"/> on, with no upper bound, because the year stays Active until it is closed. Only the
    /// calendar days count, not the time of day.
    /// </summary>
    public static bool IsYearEndReminderDue(DateTime endDate, DateTime now) =>
        now.Date >= endDate.Date.AddDays(-YearEndReminderLeadDays);

    /// <summary>
    /// Returns the reminder for the Active school year when its end is within <see cref="YearEndReminderLeadDays"/>
    /// days or has passed, or <see langword="null"/> when there is no Active year or it is not due yet. A Planned
    /// successor does not silence it: only closing the year does.
    /// </summary>
    public Task<YearEndReminder?> GetYearEndReminderAsync(CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("SchoolYearService.GetYearEndReminder", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var active = await context.SchoolYears.AsNoTracking()
                .Where(sy => sy.Status == SchoolYearStatus.Active)
                .Select(sy => new SchoolYearSummary(sy.Id, sy.Label, sy.StartDate, sy.EndDate, sy.Status))
                .FirstOrDefaultAsync(cancellationToken);

            var now = clock.Now;
            return active is not null && IsYearEndReminderDue(active.EndDate, now)
                ? new YearEndReminder(active.Id, active.Label, active.EndDate, now.Date > active.EndDate.Date)
                : null;
        });

    /// <summary>
    /// Finds the earliest Planned school year that starts after <paramref name="schoolYearId"/>, or
    /// <see langword="null"/> when there is none. The end-of-year flow uses it to resume after a restart.
    /// </summary>
    /// <exception cref="InvariantViolationException">The school year does not exist.</exception>
    public Task<SchoolYearSummary?> FindPlannedSuccessorAsync(int schoolYearId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("SchoolYearService.FindPlannedSuccessor", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var year = await context.SchoolYears.AsNoTracking()
                .FirstOrDefaultAsync(sy => sy.Id == schoolYearId, cancellationToken)
                ?? throw new InvariantViolationException($"School year {schoolYearId} does not exist.");

            var planned = await context.SchoolYears.AsNoTracking()
                .Where(sy => sy.Status == SchoolYearStatus.Planned)
                .ToListAsync(cancellationToken);

            // Filtered and ordered in memory, like the other date comparisons in this service: it is a handful of rows.
            return planned
                .Where(sy => sy.StartDate > year.StartDate)
                .OrderBy(sy => sy.StartDate)
                .Select(sy => new SchoolYearSummary(sy.Id, sy.Label, sy.StartDate, sy.EndDate, sy.Status))
                .FirstOrDefault();
        });

    /// <summary>
    /// Proposes the school year that follows <paramref name="schoolYearId"/>: the same dates one year later and
    /// the matching label. Nothing is saved.
    /// </summary>
    /// <exception cref="InvariantViolationException">The school year does not exist.</exception>
    public Task<SchoolYearDraft> ProposeNextYearAsync(int schoolYearId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("SchoolYearService.ProposeNextYear", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var previous = await context.SchoolYears.AsNoTracking()
                .FirstOrDefaultAsync(sy => sy.Id == schoolYearId, cancellationToken)
                ?? throw new InvariantViolationException($"School year {schoolYearId} does not exist.");

            var start = previous.StartDate.AddYears(1);
            return new SchoolYearDraft(FormatLabel(start.Year), start, previous.EndDate.AddYears(1));
        });

    /// <summary>
    /// Creates a Planned school year after the Active <paramref name="previousSchoolYearId"/>. Only one Planned
    /// successor may exist, so a repeated call cannot create a duplicate. The label must be unused and the dates
    /// must not overlap any existing year.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// The previous year is missing or not Active, a Planned successor already exists, the label is empty or in
    /// use, the dates are not a valid range, the range does not start after the previous year ends, or the range
    /// overlaps an existing year.
    /// </exception>
    public Task<SchoolYearSummary> CreateNextYearAsync(
        int previousSchoolYearId, SchoolYearDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return DomainOperation.RunAsync("SchoolYearService.CreateNextYear", async () =>
        {
            var label = RequiredText.Normalize(draft.Label, "A school year label");

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var previous = await context.SchoolYears.AsNoTracking()
                .FirstOrDefaultAsync(sy => sy.Id == previousSchoolYearId, cancellationToken)
                ?? throw new InvariantViolationException($"School year {previousSchoolYearId} does not exist.");

            if (previous.Status != SchoolYearStatus.Active)
            {
                throw new InvariantViolationException(
                    $"School year {previousSchoolYearId} is not Active (current status: {previous.Status}); only the Active year can get a next year.");
            }

            var existingYears = await context.SchoolYears.AsNoTracking().ToListAsync(cancellationToken);

            if (existingYears.Any(sy => sy.Status == SchoolYearStatus.Planned && sy.StartDate > previous.StartDate))
            {
                throw new InvariantViolationException(
                    $"School year {previousSchoolYearId} already has a Planned successor.");
            }

            EnsureDraftFits(previous, draft, label, existingYears);

            var schoolYear = new SchoolYear
            {
                Label = label,
                StartDate = draft.StartDate,
                EndDate = draft.EndDate,
                Status = SchoolYearStatus.Planned,
            };
            context.SchoolYears.Add(schoolYear);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            LogPlannedYearCreated();

            return new SchoolYearSummary(schoolYear.Id, schoolYear.Label, schoolYear.StartDate, schoolYear.EndDate, schoolYear.Status);
        });
    }

    /// <summary>
    /// Changes the label and the dates of a Planned school year, under the same rules as creating it. Only a Planned
    /// year can be edited: the Active year is running and a Closed year is history, even while unlocked. The new
    /// dates must not leave an assessment or grade of the year outside them (voided ones count too, because
    /// un-voiding them must stay possible).
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// The year is missing or not Planned, there is no Active year, the label is empty or in use by another year, the
    /// dates are not a valid range, the range does not start after the Active year ends, it overlaps another year, or
    /// an assessment or grade of the year would lie outside the new dates.
    /// </exception>
    public Task<SchoolYearSummary> UpdatePlannedYearAsync(
        int schoolYearId, SchoolYearDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return DomainOperation.RunAsync("SchoolYearService.UpdatePlannedYear", async () =>
        {
            var label = RequiredText.Normalize(draft.Label, "A school year label");

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var years = await context.SchoolYears.ToListAsync(cancellationToken);

            var schoolYear = years.FirstOrDefault(sy => sy.Id == schoolYearId)
                ?? throw new InvariantViolationException($"School year {schoolYearId} does not exist.");

            if (schoolYear.Status != SchoolYearStatus.Planned)
            {
                throw new InvariantViolationException(
                    $"School year {schoolYearId} is not Planned (current status: {schoolYear.Status}); only a Planned year can be edited.");
            }

            var active = years.FirstOrDefault(sy => sy.Status == SchoolYearStatus.Active)
                ?? throw new InvariantViolationException(
                    $"There is no Active school year to check school year {schoolYearId} against.");

            EnsureDraftFits(active, draft, label, [.. years.Where(sy => sy.Id != schoolYearId)]);
            await EnsureNoDatedRecordsOutsideAsync(context, schoolYearId, draft, cancellationToken);

            schoolYear.Label = label;
            schoolYear.StartDate = draft.StartDate;
            schoolYear.EndDate = draft.EndDate;

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            LogPlannedYearUpdated();

            return new SchoolYearSummary(schoolYear.Id, schoolYear.Label, schoolYear.StartDate, schoolYear.EndDate, schoolYear.Status);
        });
    }

    /// <summary>
    /// Requires a valid date range that starts after <paramref name="activeYear"/> has ended, a label that no year in
    /// <paramref name="otherYears"/> uses and dates that overlap none of them. Shared by create and edit.
    /// </summary>
    private static void EnsureDraftFits(
        SchoolYear activeYear, SchoolYearDraft draft, string label, IReadOnlyCollection<SchoolYear> otherYears)
    {
        if (draft.StartDate.Date >= draft.EndDate.Date)
        {
            throw new InvariantViolationException("The school year must start before it ends.");
        }

        if (draft.StartDate.Date <= activeYear.EndDate.Date)
        {
            throw new InvariantViolationException(
                $"The school year must start after school year {activeYear.Id} ends ({DateOnly.FromDateTime(activeYear.EndDate):O}).");
        }

        var sameLabel = otherYears.FirstOrDefault(sy => string.Equals(sy.Label.Trim(), label, StringComparison.OrdinalIgnoreCase));
        if (sameLabel is not null)
        {
            throw new InvariantViolationException($"The label is already used by school year {sameLabel.Id}.");
        }

        var overlapping = otherYears.FirstOrDefault(sy => sy.StartDate <= draft.EndDate && draft.StartDate <= sy.EndDate);
        if (overlapping is not null)
        {
            throw new InvariantViolationException($"The dates overlap school year {overlapping.Id}.");
        }
    }

    /// <summary>Requires that no assessment or grade (voided ones included) of the year lies outside the draft's dates.</summary>
    private static async Task EnsureNoDatedRecordsOutsideAsync(
        AppDbContext context, int schoolYearId, SchoolYearDraft draft, CancellationToken cancellationToken)
    {
        var start = DateOnly.FromDateTime(draft.StartDate);
        var end = DateOnly.FromDateTime(draft.EndDate);

        var hasAssessmentOutside = await context.Assessments.AsNoTracking()
            .AnyAsync(a => a.GradeBucket.SubjectInstance.ClassInstance.SchoolYearId == schoolYearId
                && (a.Date < start || a.Date > end), cancellationToken);
        var hasGradeOutside = !hasAssessmentOutside && await context.Grades.AsNoTracking()
            .AnyAsync(g => g.GradeBucket.SubjectInstance.ClassInstance.SchoolYearId == schoolYearId
                && (g.RecordedOn < start || g.RecordedOn > end), cancellationToken);

        if (hasAssessmentOutside || hasGradeOutside)
        {
            throw new InvariantViolationException(
                $"School year {schoolYearId} has assessments or grades dated outside {start:O} to {end:O}.");
        }
    }

    /// <summary>Formats a label such as "2025/26" from the calendar year in which the school year starts.</summary>
    private static string FormatLabel(int startYear) => $"{startYear}/{(startYear + 1) % 100:D2}";

    [LoggerMessage(Level = LogLevel.Information, Message = "Created an Active school year")]
    private partial void LogActiveYearCreated();

    [LoggerMessage(Level = LogLevel.Information, Message = "Created a Planned school year")]
    private partial void LogPlannedYearCreated();

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated a Planned school year")]
    private partial void LogPlannedYearUpdated();
}
