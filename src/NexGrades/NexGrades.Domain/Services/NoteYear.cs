using NexGrades.Data.Entities;

namespace NexGrades.Domain.Services;

/// <summary>
/// A school year a student has an enrollment in, together with whether notes may be written to it right now.
/// </summary>
/// <param name="SchoolYearId">The school year id.</param>
/// <param name="Label">The label of the school year, e.g. "2025/26".</param>
/// <param name="Status">The lifecycle status of the school year.</param>
/// <param name="UnlockedUntil">When a temporary unlock of a closed year ends, if there is one.</param>
/// <param name="IsWritable">
/// False for a closed year that is not unlocked; see <see cref="Auditing.SchoolYearLock.IsWritable"/>.
/// </param>
public sealed record NoteYear(
    int SchoolYearId,
    string Label,
    SchoolYearStatus Status,
    DateTime? UnlockedUntil,
    bool IsWritable);
