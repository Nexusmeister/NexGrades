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
    void SetReason(int schoolYearId, string reason);

    void ClearReason(int schoolYearId);

    string? GetReason(int schoolYearId);
}
