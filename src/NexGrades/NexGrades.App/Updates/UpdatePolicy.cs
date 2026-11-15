using NexGrades.App.Infrastructure;
using Velopack;

namespace NexGrades.App.Updates;

/// <summary>
/// The rules for which channels are checked and which update is taken. Kept apart from the coordinator so they can be
/// tested on their own.
/// </summary>
internal static class UpdatePolicy
{
    /// <summary>The stable channel always; the beta channel as well when the user opted in.</summary>
    public static IReadOnlyList<UpdateChannel> ChannelsToCheck(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.ReceiveBetaUpdates ? [UpdateChannel.Stable, UpdateChannel.Beta] : [UpdateChannel.Stable];
    }

    /// <summary>
    /// The highest candidate that is newer than <paramref name="current"/> and not a downgrade, or
    /// <see langword="null"/>. So a beta never moves to an older stable release (0.1.2-beta.4 stays put when 0.1.1 is
    /// the latest stable) but does move to the stable release of its own version (0.1.2). On a tie the earlier
    /// candidate wins, which is the stable one.
    /// </summary>
    public static UpdateCandidate? Choose(SemanticVersion current, IEnumerable<UpdateCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candidates);

        UpdateCandidate? best = null;
        foreach (var candidate in candidates)
        {
            if (!candidate.IsDowngrade && candidate.Version > current && (best is null || candidate.Version > best.Version))
            {
                best = candidate;
            }
        }

        return best;
    }
}
