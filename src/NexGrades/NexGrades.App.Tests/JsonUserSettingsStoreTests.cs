using System.IO;
using Microsoft.Extensions.Logging;
using NexGrades.App.Infrastructure;
using NexGrades.App.Storage;

namespace NexGrades.App.Tests;

public sealed class JsonUserSettingsStoreTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly CapturingLogger<JsonUserSettingsStore> _logger = new();
    private readonly AppDataPaths _paths;
    private readonly JsonUserSettingsStore _store;

    public JsonUserSettingsStoreTests()
    {
        _paths = new AppDataPaths(Path.Combine(_folder.Path, "data"));
        _store = new JsonUserSettingsStore(_paths, _logger);
    }

    public void Dispose()
    {
        _store.Dispose();
        _folder.Dispose();
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefaults_WhenFileIsMissing()
    {
        var settings = await _store.LoadAsync();

        Assert.Equal(new UserSettings(), settings);
        Assert.False(Directory.Exists(_paths.DataDirectory));
    }

    [Fact]
    public void UserSettings_DefaultsToAutoCheckOnAndBetaOff()
    {
        var settings = new UserSettings();

        Assert.True(settings.AutoCheckForUpdates);
        Assert.False(settings.ReceiveBetaUpdates);
        Assert.Null(settings.LastRunVersion);
    }

    [Fact]
    public async Task UpdateAsync_PersistsValues_ThatLoadAsyncReadsBack()
    {
        _ = await _store.UpdateAsync(s => s with { AutoCheckForUpdates = false, ReceiveBetaUpdates = true, LastRunVersion = "0.1.2-beta.4" });

        using var fresh = new JsonUserSettingsStore(_paths, _logger);
        var loaded = await fresh.LoadAsync();

        Assert.False(loaded.AutoCheckForUpdates);
        Assert.True(loaded.ReceiveBetaUpdates);
        Assert.Equal("0.1.2-beta.4", loaded.LastRunVersion);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsTheSettingsAsWritten()
    {
        var written = await _store.UpdateAsync(s => s with { ReceiveBetaUpdates = true });

        Assert.True(written.ReceiveBetaUpdates);
        Assert.Equal(written, await _store.LoadAsync());
    }

    [Fact]
    public async Task UpdateAsync_CreatesTheDataFolder_WhenMissing()
    {
        Assert.False(Directory.Exists(_paths.DataDirectory));

        _ = await _store.UpdateAsync(s => s with { ReceiveBetaUpdates = true });

        Assert.True(File.Exists(_paths.SettingsFilePath));
    }

    [Fact]
    public async Task UpdateAsync_LeavesNoTemporaryFile()
    {
        _ = await _store.UpdateAsync(s => s with { ReceiveBetaUpdates = true });
        _ = await _store.UpdateAsync(s => s with { AutoCheckForUpdates = false });

        var files = Directory.EnumerateFileSystemEntries(_paths.DataDirectory).Select(Path.GetFileName).ToList();
        Assert.Equal([AppDataPaths.SettingsFileName], files);
    }

    [Fact]
    public async Task UpdateAsync_KeepsBothChanges_WhenCallersChangeDifferentSettingsAtOnce()
    {
        await Task.WhenAll(
            Enumerable.Range(0, 10).Select(i => i % 2 == 0
                ? _store.UpdateAsync(s => s with { AutoCheckForUpdates = false })
                : _store.UpdateAsync(s => s with { ReceiveBetaUpdates = true })));

        var loaded = await _store.LoadAsync();
        Assert.False(loaded.AutoCheckForUpdates);
        Assert.True(loaded.ReceiveBetaUpdates);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{\"autoCheckForUpdates\": \"maybe\"}")]
    public async Task LoadAsync_ReturnsDefaultsWithoutThrowing_WhenFileIsCorrupt(string content)
    {
        _ = Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.SettingsFilePath, content);

        var settings = await _store.LoadAsync();

        Assert.Equal(new UserSettings(), settings);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefaults_WhenFileHoldsJsonNull()
    {
        _ = Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.SettingsFilePath, "null");

        Assert.Equal(new UserSettings(), await _store.LoadAsync());
    }

    [Fact]
    public async Task LoadAsync_LogsNothingFromTheFile_WhenFileIsCorrupt()
    {
        _ = Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.SettingsFilePath, "{ secret-content-Zebra42");

        _ = await _store.LoadAsync();

        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("Zebra42", StringComparison.Ordinal) || e.Message.Contains(_folder.Path, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateAsync_ReplacesACorruptFile_WithValidSettings()
    {
        _ = Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.SettingsFilePath, "{ broken");

        _ = await _store.UpdateAsync(s => s with { ReceiveBetaUpdates = true });

        using var fresh = new JsonUserSettingsStore(_paths, _logger);
        Assert.True((await fresh.LoadAsync()).ReceiveBetaUpdates);
    }

    [Fact]
    public async Task LoadAsync_UsesDefaultForAMissingProperty()
    {
        _ = Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.SettingsFilePath, "{\"receiveBetaUpdates\": true}");

        var settings = await _store.LoadAsync();

        Assert.True(settings.ReceiveBetaUpdates);
        Assert.True(settings.AutoCheckForUpdates);
        Assert.Null(settings.LastRunVersion);
    }

    [Fact]
    public async Task LoadAsync_UsesDefaults_ForAnEmptyObject()
    {
        _ = Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.SettingsFilePath, "{}");

        Assert.Equal(new UserSettings(), await _store.LoadAsync());
    }

    [Fact]
    public async Task LoadAsync_IgnoresUnknownProperties()
    {
        _ = Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.SettingsFilePath, "{\"futureSetting\": 5, \"autoCheckForUpdates\": false}");

        var settings = await _store.LoadAsync();

        Assert.False(settings.AutoCheckForUpdates);
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenUpdateIsNull()
    {
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => _store.UpdateAsync(null!));
    }
}
