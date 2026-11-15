using System.Reflection;

namespace NexGrades.App.Infrastructure;

/// <summary>
/// The version of the running app as MinVer stamps it (for example <c>0.2.0</c> or <c>0.2.1-beta.3</c>), without the
/// <c>+commit</c> build metadata. It is the version shown in Settings, written to the log and recorded as the last
/// version that ran against the database, so it matches the version the release and the updater use.
/// </summary>
public static class AppVersion
{
    /// <summary>The value used when the assembly carries no informational version.</summary>
    public const string Unknown = "unknown";

    /// <summary>The informational version of the app without build metadata, or <see cref="Unknown"/>.</summary>
    public static string Current { get; } = Read();

    private static string Read()
    {
        var informational = typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
        {
            return Unknown;
        }

        var metadata = informational.IndexOf('+', StringComparison.Ordinal);
        return metadata < 0 ? informational : informational[..metadata];
    }
}
