using Velopack;

namespace NexGrades.App.Updates;

/// <summary>
/// An update a channel offers. <see cref="IUpdateClient"/> creates it and is the only one that reads
/// <see cref="Handle"/>; everything else decides on the version, the channel and whether it is a downgrade.
/// </summary>
/// <param name="Version">The version the update installs.</param>
/// <param name="Channel">The channel that offered it.</param>
/// <param name="IsDowngrade">True when installing it would not move to a newer version; such an update is never chosen.</param>
public sealed record UpdateCandidate(SemanticVersion Version, UpdateChannel Channel, bool IsDowngrade)
{
    /// <summary>The client's own state for downloading and applying this update; opaque to everyone else.</summary>
    internal object? Handle { get; init; }
}
