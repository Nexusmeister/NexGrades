namespace NexGrades.App.Updates;

/// <summary>
/// Where updates come from, bound from the <c>Updates</c> section of <c>appsettings.json</c>.
/// </summary>
public sealed class UpdateFeedOptions
{
    /// <summary>The configuration section the options are bound from.</summary>
    public const string SectionName = "Updates";

    /// <summary>The GitHub repository whose releases carry the update packages, for example <c>https://github.com/owner/repo</c>.</summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>
    /// For testing an update end to end: a local folder or an http(s) URL holding the release files, used for every
    /// channel instead of <see cref="Repository"/>. Empty in a shipped build.
    /// </summary>
    public string? SourceOverride { get; set; }

    /// <summary>How long the check of one channel may take before it counts as failed. 30 seconds by default.</summary>
    public TimeSpan CheckTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
