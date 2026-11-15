using System.Globalization;

namespace NexGrades.Domain.Maintenance;

/// <summary>
/// Names of the database backups taken before a migration: <c>app-yyyyMMdd-HHmmss-v&lt;version&gt;.db</c>, or
/// <c>app-yyyyMMdd-HHmmss-unknown.db</c> when the version that wrote the database is not known. The timestamp in the
/// name, not the file's write time, orders the backups, so copying the folder elsewhere keeps the order.
/// </summary>
public static class DatabaseBackupFileName
{
    /// <summary>The start of every backup file name.</summary>
    public const string Prefix = "app-";

    /// <summary>The extension of every backup file.</summary>
    public const string Extension = ".db";

    /// <summary>The name part used when the app version is unknown or not safe to put in a file name.</summary>
    public const string UnknownVersion = "unknown";

    private const string TimestampFormat = "yyyyMMdd-HHmmss";

    /// <summary>
    /// Builds the file name for a backup taken at <paramref name="timestamp"/> of a database last used by
    /// <paramref name="appVersion"/>. A version that is empty or holds characters other than letters, digits,
    /// <c>.</c> and <c>-</c> is written as <see cref="UnknownVersion"/>, so the name is always a valid file name.
    /// </summary>
    /// <param name="timestamp">When the backup is taken, in local time.</param>
    /// <param name="appVersion">The app version that last ran against the database, or <see langword="null"/>.</param>
    /// <returns>The file name, without a folder.</returns>
    public static string Create(DateTime timestamp, string? appVersion)
    {
        var stamp = timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var version = IsSafeVersion(appVersion) ? "v" + appVersion : UnknownVersion;
        return string.Concat(Prefix, stamp, "-", version, Extension);
    }

    /// <summary>
    /// Reads the timestamp from a backup file name made by <see cref="Create"/>, including names with a collision
    /// suffix such as <c>-1</c> before the extension. Any other file name is not a backup.
    /// </summary>
    /// <param name="fileName">A file name without a folder.</param>
    /// <param name="timestamp">The timestamp in the name, or <see langword="default"/> when the name is not a backup.</param>
    /// <returns><see langword="true"/> when <paramref name="fileName"/> is a backup file name.</returns>
    public static bool TryParse(string? fileName, out DateTime timestamp)
    {
        timestamp = default;
        if (fileName is null
            || !fileName.StartsWith(Prefix, StringComparison.Ordinal)
            || !fileName.EndsWith(Extension, StringComparison.Ordinal))
        {
            return false;
        }

        var stampStart = Prefix.Length;
        var restStart = stampStart + TimestampFormat.Length + 1;
        var restLength = fileName.Length - Extension.Length - restStart;
        if (restLength <= 0 || fileName[restStart - 1] != '-')
        {
            return false;
        }

        var rest = fileName.AsSpan(restStart, restLength);
        var hasVersion = rest.StartsWith(UnknownVersion, StringComparison.Ordinal) || (rest.Length > 1 && rest[0] == 'v');
        if (!hasVersion || !IsSafe(rest))
        {
            return false;
        }

        return DateTime.TryParseExact(
            fileName.AsSpan(stampStart, TimestampFormat.Length),
            TimestampFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out timestamp);
    }

    private static bool IsSafeVersion(string? appVersion) => !string.IsNullOrEmpty(appVersion) && IsSafe(appVersion);

    private static bool IsSafe(ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '.' && c != '-')
            {
                return false;
            }
        }

        return true;
    }
}
