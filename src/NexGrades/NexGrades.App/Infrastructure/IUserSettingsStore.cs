using System.IO;

namespace NexGrades.App.Infrastructure;

/// <summary>
/// Reads and writes <see cref="UserSettings"/>. Every change goes through <see cref="UpdateAsync"/>, which reads,
/// changes and writes in one step, so two callers that change different settings never lose each other's change.
/// </summary>
public interface IUserSettingsStore
{
    /// <summary>Reads the settings. A missing or unreadable file gives the defaults.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored settings, or the defaults.</returns>
    /// <exception cref="IOException">The file exists but could not be read.</exception>
    Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads the settings, applies <paramref name="update"/> and writes the result.</summary>
    /// <param name="update">Returns the changed settings, typically with a <see langword="with"/> expression.</param>
    /// <param name="cancellationToken">Cancels the update before it writes.</param>
    /// <returns>The settings as written.</returns>
    /// <exception cref="IOException">The file could not be written.</exception>
    Task<UserSettings> UpdateAsync(Func<UserSettings, UserSettings> update, CancellationToken cancellationToken = default);
}
