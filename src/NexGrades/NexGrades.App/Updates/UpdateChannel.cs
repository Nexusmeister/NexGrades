namespace NexGrades.App.Updates;

/// <summary>
/// A release channel. Each channel has its own release feed (<c>releases.stable.json</c>, <c>releases.beta.json</c>),
/// and the names here are the channel names the release workflow packs with.
/// </summary>
public enum UpdateChannel
{
    /// <summary>Stable releases; always checked.</summary>
    Stable,

    /// <summary>Beta pre-releases; checked only when the user opted in.</summary>
    Beta,
}
