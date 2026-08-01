using System.Collections.Concurrent;

namespace NexGrades.Domain.Auditing;

/// <summary>Default in-memory <see cref="IUnlockReasonStore"/>. Register as a singleton.</summary>
public sealed class UnlockReasonStore : IUnlockReasonStore
{
    private readonly ConcurrentDictionary<int, string> _reasonsBySchoolYearId = new();

    public void SetReason(int schoolYearId, string reason) => _reasonsBySchoolYearId[schoolYearId] = reason;

    public void ClearReason(int schoolYearId) => _reasonsBySchoolYearId.TryRemove(schoolYearId, out _);

    public string? GetReason(int schoolYearId) =>
        _reasonsBySchoolYearId.TryGetValue(schoolYearId, out var reason) ? reason : null;
}
