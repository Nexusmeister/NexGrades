using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Time;

namespace NexGrades.Domain.Services;

/// <summary>
/// School-year lifecycle rules. Today the service only holds the startup bootstrap of an Active year; the planned
/// UI flow to start a new school year and to close a year and roll over (#72) will extend it.
/// </summary>
/// <param name="contextFactory">Creates the short-lived context that each call opens.</param>
/// <param name="clock">Source of "now", used to pick the school year that contains today.</param>
/// <param name="logger">Receives a content-free line when a year is created.</param>
/// <remarks>Opens a short-lived context per call; see <see cref="EnrollmentService"/> for why.</remarks>
public sealed partial class SchoolYearService(IDbContextFactory<AppDbContext> contextFactory, IClock clock, ILogger<SchoolYearService> logger)
{
    /// <summary>
    /// Makes sure an Active school year exists. Everything in the model is scoped through a
    /// <see cref="SchoolYear"/>, but there is no year-management UI yet. Rather than block every other screen on
    /// that being built first, this bootstraps one Active year so Students/Classes/Subjects keep working. It is a
    /// stopgap for a real "start a new school year" workflow, not a substitute for one. Does nothing when any year
    /// is already Active; Planned or Closed years alone do not prevent creating one.
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
                Label = $"{startYear}/{(startYear + 1) % 100:D2}",
                StartDate = new DateTime(startYear, 8, 1),
                EndDate = new DateTime(startYear + 1, 7, 31),
                Status = SchoolYearStatus.Active,
            });

            await context.SaveChangesAsync(cancellationToken);
            LogActiveYearCreated();
        });

    [LoggerMessage(Level = LogLevel.Information, Message = "Created an Active school year")]
    private partial void LogActiveYearCreated();
}
