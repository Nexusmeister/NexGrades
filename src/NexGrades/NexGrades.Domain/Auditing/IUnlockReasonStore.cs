namespace NexGrades.Domain.Auditing;

/// <summary>
/// Ambient store carrying the reason given for unlocking a closed school year, keyed by school year, from
/// <see cref="Services.UnlockService.UnlockAsync"/> — where the teacher supplies it — through to
/// <see cref="ClosedYearAuditInterceptor"/> — which reads it when writing each <see cref="Data.Entities.AuditEntry"/>.
/// <c>SaveChanges</c> takes no extra parameters, so this small shared store is the mechanism that carries the
/// reason across that gap. Registered as a singleton: this is a single-teacher desktop app, so a process-wide
/// map from school year to "why is this unlocked right now" is sufficient and keeps both sides trivially
/// testable without touching ambient/async-local state.
/// </summary>
public interface IUnlockReasonStore
{
    /// <summary>Records the reason for the current unlock of a school year, replacing any earlier reason.</summary>
    /// <param name="schoolYearId">The closed school year that is being unlocked.</param>
    /// <param name="reason">Why the teacher unlocked the year. It is written to every audit entry.</param>
    void SetReason(int schoolYearId, string reason);

    /// <summary>Forgets the reason for a school year. <c>UnlockService.RelockAsync</c> calls this when the year is re-locked.</summary>
    /// <param name="schoolYearId">The school year whose reason is removed.</param>
    void ClearReason(int schoolYearId);

    /// <summary>Returns the stored reason for a school year, or <see langword="null"/> when none is stored.</summary>
    /// <param name="schoolYearId">The school year to look up.</param>
    string? GetReason(int schoolYearId);
}
