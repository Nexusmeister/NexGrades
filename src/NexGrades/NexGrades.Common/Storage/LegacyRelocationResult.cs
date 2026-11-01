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
