using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Time;

namespace NexGrades.Domain.Services;

/// <summary>
/// Deliberate, visible, reversible unlocking of a closed school year. Unlocking sets
/// <see cref="SchoolYear.UnlockedUntil"/> to a future timestamp, the mechanism
/// <see cref="Auditing.ClosedYearAuditInterceptor"/> checks before allowing a write to anything reachable
/// from that year, and stores the reason in <see cref="SchoolYear.UnlockReason"/> so the interceptor can attach it
/// to every <see cref="AuditEntry"/> written while unlocked. Re-locking clears both immediately. Unlocking and
/// re-locking are not audited themselves: a <see cref="SchoolYear"/> write is outside the audit mechanism.
/// </summary>
/// <param name="contextFactory">Creates the short-lived context that each call opens.</param>
/// <param name="clock">Source of "now", used to reject an unlock that would expire in the past.</param>
/// <param name="logger">Receives fixed, argument-less messages; no school year data is logged.</param>
/// <remarks>Opens a short-lived context per call; see <see cref="EnrollmentService"/> for why.</remarks>
public sealed partial class UnlockService(IDbContextFactory<AppDbContext> contextFactory, IClock clock, ILogger<UnlockService> logger)
{
    /// <summary>The longest unlock reason accepted, matching the column length of <see cref="SchoolYear.UnlockReason"/>.</summary>
    public const int MaxReasonLength = 500;

    /// <summary>
    /// Unlocks <paramref name="schoolYearId"/> until <paramref name="until"/>, recording <paramref name="reason"/>
    /// for the audit trail. Only valid for a year that is currently <see cref="SchoolYearStatus.Closed"/>. The reason
    /// is trimmed and must not be empty or longer than <see cref="MaxReasonLength"/>.
    /// </summary>
    public Task UnlockAsync(int schoolYearId, DateTime until, string reason, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("UnlockService.Unlock", async () =>
        {
            var normalizedReason = RequiredText.Normalize(reason, "An unlock reason", MaxReasonLength);

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var schoolYear = await context.SchoolYears.FirstOrDefaultAsync(sy => sy.Id == schoolYearId, cancellationToken)
                ?? throw new InvariantViolationException($"School year {schoolYearId} does not exist.");

            if (schoolYear.Status != SchoolYearStatus.Closed)
            {
                throw new InvariantViolationException(
                    $"School year {schoolYearId} is not closed (current status: {schoolYear.Status}); only a closed year can be unlocked.");
            }

            if (until <= clock.Now)
            {
                throw new InvariantViolationException($"Unlock expiry {until:O} must be in the future.");
            }

            schoolYear.UnlockedUntil = until;
            schoolYear.UnlockReason = normalizedReason;

            await context.SaveChangesAsync(cancellationToken);
            LogYearUnlocked();
        });

    /// <summary>
    /// Re-locks <paramref name="schoolYearId"/> immediately: clears <see cref="SchoolYear.UnlockedUntil"/>
    /// and <see cref="SchoolYear.UnlockReason"/>. Re-locking a year that is not unlocked changes nothing.
    /// </summary>
    public Task RelockAsync(int schoolYearId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("UnlockService.Relock", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var schoolYear = await context.SchoolYears.FirstOrDefaultAsync(sy => sy.Id == schoolYearId, cancellationToken)
                ?? throw new InvariantViolationException($"School year {schoolYearId} does not exist.");

            schoolYear.UnlockedUntil = null;
            schoolYear.UnlockReason = null;

            await context.SaveChangesAsync(cancellationToken);
            LogYearRelocked();
        });

    /// <summary>Reads the lock fields of one school year, for showing its lock state.</summary>
    /// <exception cref="InvariantViolationException">The school year does not exist.</exception>
    public Task<SchoolYearLockState> GetLockStateAsync(int schoolYearId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("UnlockService.GetLockState", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var state = await context.SchoolYears
                .AsNoTracking()
                .Where(sy => sy.Id == schoolYearId)
                .Select(sy => new SchoolYearLockState(sy.Id, sy.Label, sy.Status, sy.ClosedOn, sy.UnlockedUntil, sy.UnlockReason))
                .FirstOrDefaultAsync(cancellationToken);

            return state ?? throw new InvariantViolationException($"School year {schoolYearId} does not exist.");
        });

    /// <summary>Lists every <see cref="SchoolYearStatus.Closed"/> school year, newest first, with its lock state.</summary>
    public Task<IReadOnlyList<SchoolYearLockState>> GetClosedYearsAsync(CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("UnlockService.GetClosedYears", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var years = await context.SchoolYears
                .AsNoTracking()
                .Where(sy => sy.Status == SchoolYearStatus.Closed)
                .OrderByDescending(sy => sy.StartDate)
                .Select(sy => new SchoolYearLockState(sy.Id, sy.Label, sy.Status, sy.ClosedOn, sy.UnlockedUntil, sy.UnlockReason))
                .ToListAsync(cancellationToken);

            return (IReadOnlyList<SchoolYearLockState>)years;
        });

    [LoggerMessage(Level = LogLevel.Information, Message = "Unlocked a closed school year")]
    private partial void LogYearUnlocked();

    [LoggerMessage(Level = LogLevel.Information, Message = "Re-locked a school year")]
    private partial void LogYearRelocked();
}
