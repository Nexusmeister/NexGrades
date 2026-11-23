using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NexGrades.App.Storage;

namespace NexGrades.App.Infrastructure;

/// <summary>
/// Keeps <see cref="UserSettings"/> as JSON in <see cref="IAppDataPaths.SettingsFilePath"/>. A write goes to a temporary
/// file in the same folder first and then replaces the old file, so a crash mid-write never leaves a half-written
/// file. A corrupt file is read as the defaults and logged with a constant message; the next save replaces it.
/// Nothing is read or written until the first call.
/// </summary>
/// <param name="paths">Locates the settings file.</param>
/// <param name="logger">Logs a corrupt file; never its content or path.</param>
public sealed partial class JsonUserSettingsStore(IAppDataPaths paths, ILogger<JsonUserSettingsStore> logger) : IUserSettingsStore, IDisposable
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <inheritdoc />
    public async Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadAsync(cancellationToken);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<UserSettings> UpdateAsync(Func<UserSettings, UserSettings> update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var settings = update(await ReadAsync(cancellationToken));
            await WriteAsync(settings, cancellationToken);
            return settings;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>Releases the lock that serialises reads and writes.</summary>
    public void Dispose() => _gate.Dispose();

    private async Task<UserSettings> ReadAsync(CancellationToken cancellationToken)
    {
        var path = paths.SettingsFilePath;
        if (!File.Exists(path))
        {
            return new UserSettings();
        }

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            return await JsonSerializer.DeserializeAsync<UserSettings>(stream, _jsonOptions, cancellationToken) ?? new UserSettings();
        }
        catch (JsonException)
        {
            LogCorruptSettings();
            return new UserSettings();
        }
        catch (FileNotFoundException)
        {
            return new UserSettings();
        }
    }

    private async Task WriteAsync(UserSettings settings, CancellationToken cancellationToken)
    {
        var path = paths.SettingsFilePath;
        var folder = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("The settings file path has no folder.");
        _ = Directory.CreateDirectory(folder);

        var temporaryPath = Path.Combine(folder, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions, cancellationToken);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The temporary file is left behind; it never replaces the settings file.
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The settings file could not be read; using the default settings")]
    private partial void LogCorruptSettings();
}
