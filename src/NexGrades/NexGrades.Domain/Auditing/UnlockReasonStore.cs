using System.Collections.Concurrent;

namespace NexGrades.Domain.Auditing;

/// <summary>Default in-memory <see cref="IUnlockReasonStore"/>. Register as a singleton.</summary>
public sealed class UnlockReasonStore : IUnlockReasonStore
{
    private readonly ConcurrentDictionary<int, string> _reasonsBySchoolYearId = new();

    /// <summary>Stores the reason for the school year, replacing any earlier one.</summary>
    public void SetReason(int schoolYearId, string reason) => _reasonsBySchoolYearId[schoolYearId] = reason;

    /// <summary>Removes the stored reason for the school year, if there is one.</summary>
    public void ClearReason(int schoolYearId) => _reasonsBySchoolYearId.TryRemove(schoolYearId, out _);

    /// <summary>Returns the stored reason for the school year, or <see langword="null"/> when there is none.</summary>
    public string? GetReason(int schoolYearId) =>
        _reasonsBySchoolYearId.TryGetValue(schoolYearId, out var reason) ? reason : null;
}
