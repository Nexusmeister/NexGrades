namespace NexGrades.Common.Storage;

/// <summary>Outcome of <see cref="LegacyDatabaseRelocator.Relocate"/>.</summary>
public enum LegacyRelocationResult
{
    /// <summary>No legacy database was found.</summary>
    NotFound,

    /// <summary>A database already exists at the target; nothing was touched.</summary>
    TargetExists,

    /// <summary>The legacy database was copied to the target and the old files removed.</summary>
    Moved,

    /// <summary>Copied to the target, but the old files could not be removed (e.g. read-only folder).</summary>
    MovedSourceKept,
}

/// <summary>
/// One-time migration of an <c>app.db</c> that older versions created next to the executable (or in the
/// working directory) into the per-user data folder. It never overwrites an existing target, and copies
/// before deleting, so a failure at any point cannot lose the user's data.
/// </summary>
public sealed class LegacyDatabaseRelocator
{
    private static readonly string[] _sidecarSuffixes = ["-wal", "-shm", "-journal"];

    /// <param name="candidateDirectories">Directories to look in, in priority order; the first holding the file wins.</param>
    /// <param name="targetDatabasePath">Full path of the new database file; its file name is the legacy name too.</param>
    public LegacyRelocationResult Relocate(IEnumerable<string> candidateDirectories, string targetDatabasePath)
    {
        var target = Path.GetFullPath(targetDatabasePath);
        if (File.Exists(target))
        {
            return LegacyRelocationResult.TargetExists;
        }

        var targetDirectory = Path.GetDirectoryName(target)!;
        var source = candidateDirectories
            .Select(d => Path.GetFullPath(Path.Combine(d, Path.GetFileName(target))))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(IsNexGradesDatabase);
        if (source is null)
        {
            return LegacyRelocationResult.NotFound;
        }

        Directory.CreateDirectory(targetDirectory);

        // Sidecars first, main file last: a crash midway leaves no target main file, so the next start retries.
        var pairs = _sidecarSuffixes
            .Select(s => (From: source + s, To: target + s))
            .Where(p => File.Exists(p.From))
            .Append((From: source, To: target))
            .ToList();

        var created = new List<string>();
        try
        {
            foreach (var (from, to) in pairs)
            {
                File.Copy(from, to, overwrite: true);
                created.Add(to);
            }
        }
        catch
        {
            foreach (var path in created)
            {
                TryDelete(path);
            }

            throw;
        }

        var allRemoved = true;
        foreach (var (from, _) in pairs)
        {
            allRemoved &= TryDelete(from);
        }

        return allRemoved ? LegacyRelocationResult.Moved : LegacyRelocationResult.MovedSourceKept;
    }

    /// <summary>
    /// The working directory may hold an unrelated <c>app.db</c>, which must not be taken over. Accept only an
    /// SQLite file whose first 64 KB (where the schema table lives for a small database) mention EF's
    /// migrations history table. Needs no SQLite dependency and does not open a connection.
    /// </summary>
    private static bool IsNexGradesDatabase(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[64 * 1024];
            var read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            var head = buffer.AsSpan(0, read);
            return head.StartsWith("SQLite format 3\0"u8) && head.IndexOf("__EFMigrationsHistory"u8) >= 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
