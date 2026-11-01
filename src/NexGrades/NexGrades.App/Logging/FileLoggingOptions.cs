using Microsoft.Extensions.Configuration;

namespace NexGrades.App.Logging;

/// <summary>
/// How the rolling log files are bounded: at most <paramref name="RetainedFileCount"/> files of
/// <paramref name="MaxFileSizeMegabytes"/> MB each are kept, so the log folder cannot grow without limit.
/// </summary>
/// <param name="RetainedFileCount">How many log files to keep before the oldest is removed.</param>
/// <param name="MaxFileSizeMegabytes">Size at which the current file rolls over to a new one.</param>
public sealed record FileLoggingOptions(int RetainedFileCount = 14, int MaxFileSizeMegabytes = 3)
{
    /// <summary>The configuration section the options are read from.</summary>
    public const string SectionName = "Logging:File";

    /// <summary>Returns the options with every value raised to at least 1, so a bad setting cannot disable rolling.</summary>
    public FileLoggingOptions Normalize() =>
        new(Math.Max(1, RetainedFileCount), Math.Max(1, MaxFileSizeMegabytes));

    /// <summary>Reads the options from <see cref="SectionName"/>; missing values keep their defaults.</summary>
    /// <param name="configuration">The app configuration.</param>
    public static FileLoggingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(SectionName);
        var defaults = new FileLoggingOptions();
        return new FileLoggingOptions(
            section.GetValue("RetainedFileCount", defaults.RetainedFileCount),
            section.GetValue("MaxFileSizeMegabytes", defaults.MaxFileSizeMegabytes)).Normalize();
    }
}
