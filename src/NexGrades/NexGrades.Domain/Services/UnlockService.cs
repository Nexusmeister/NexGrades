using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Auditing;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Time;

namespace NexGrades.Domain.Services;

/// <summary>
/// Deliberate, visible, reversible unlocking of a closed school year. Unlocking sets
/// <see cref="SchoolYear.UnlockedUntil"/> to a future timestamp — the mechanism
/// <see cref="Auditing.ClosedYearAuditInterceptor"/> checks before allowing a write to anything reachable
/// from that year — and records the reason in <see cref="IUnlockReasonStore"/> so it can be attached to
/// every <see cref="AuditEntry"/> written while unlocked. Re-locking clears the window immediately.
/// </summary>
public sealed class UnlockService(AppDbContext context, IClock clock, IUnlockReasonStore reasonStore)
{
    /// <summary>
    /// Unlocks <paramref name="schoolYearId"/> until <paramref name="until"/>, recording <paramref name="reason"/>
    /// for the audit trail. Only valid for a year that is currently <see cref="SchoolYearStatus.Closed"/>.
    /// </summary>
    public async Task UnlockAsync(int schoolYearId, DateTime until, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to unlock a closed school year.", nameof(reason));
        }

        var schoolYear = await context.SchoolYears.FirstOrDefaultAsync(sy => sy.Id == schoolYearId, cancellationToken)
            ?? throw new InvariantViolationException($"School year {schoolYearId} does not exist.");

        if (schoolYear.Status != SchoolYearStatus.Closed)
        {
            throw new InvariantViolationException(
                $"School year {schoolYearId} is not closed (current status: {schoolYear.Status}); only a closed year can be unlocked.");
        }

        if (until <= clock.Now)
        {
            throw new ArgumentOutOfRangeException(nameof(until), until, "Unlock expiry must be in the future.");
        }

        schoolYear.UnlockedUntil = until;

        await context.SaveChangesAsync(cancellationToken);

        // Only recorded after the save succeeds: the interceptor independently re-checks UnlockedUntil from
        // the database before trusting this reason, but setting it earlier would leave a stale reason in the
        // store for a year that a failed save never actually unlocked.
        reasonStore.SetReason(schoolYearId, reason);
    }

    /// <summary>
    /// Re-locks <paramref name="schoolYearId"/> immediately: clears <see cref="SchoolYear.UnlockedUntil"/>
    /// and forgets the stored unlock reason.
    /// </summary>
    public async Task RelockAsync(int schoolYearId, CancellationToken cancellationToken = default)
    {
        var schoolYear = await context.SchoolYears.FirstOrDefaultAsync(sy => sy.Id == schoolYearId, cancellationToken)
            ?? throw new InvariantViolationException($"School year {schoolYearId} does not exist.");

        schoolYear.UnlockedUntil = null;
        reasonStore.ClearReason(schoolYearId);

        await context.SaveChangesAsync(cancellationToken);
    }
}
