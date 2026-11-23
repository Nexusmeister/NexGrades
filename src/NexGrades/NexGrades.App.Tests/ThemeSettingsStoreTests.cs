using System.IO;
using System.Text.Json;
using NexGrades.App.Infrastructure;
using NexGrades.App.Storage;
using NexGrades.App.Theming;

namespace NexGrades.App.Tests;

public sealed class ThemeSettingsStoreTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly AppDataPaths _paths;
    private readonly JsonUserSettingsStore _store;

    public ThemeSettingsStoreTests()
    {
        _paths = new AppDataPaths(Path.Combine(_folder.Path, "data"));
        _store = new JsonUserSettingsStore(_paths, new CapturingLogger<JsonUserSettingsStore>());
    }

    public void Dispose()
    {
        _store.Dispose();
        _folder.Dispose();
    }

    [Fact]
    public void UserSettings_DefaultsToSystemTheme()
    {
        Assert.Equal(ThemePreference.System, new UserSettings().Theme);
    }

    [Theory]
    [InlineData(ThemePreference.Dark)]
    [InlineData(ThemePreference.Light)]
    public async Task LoadAsync_ReadsBackTheme_AfterUpdateAsync(ThemePreference theme)
    {
        _ = await _store.UpdateAsync(s => s with { Theme = theme });

        using var fresh = new JsonUserSettingsStore(_paths, new CapturingLogger<JsonUserSettingsStore>());
        Assert.Equal(theme, (await fresh.LoadAsync()).Theme);
    }

    [Fact]
    public async Task UpdateAsync_StoresThemeByName_InTheFile()
    {
        _ = await _store.UpdateAsync(s => s with { Theme = ThemePreference.Dark });

        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(_paths.SettingsFilePath));
        Assert.Equal("Dark", json.RootElement.GetProperty("theme").GetString());
    }

    [Fact]
    public async Task LoadAsync_ReadsLowerCaseThemeName()
    {
        _ = Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.SettingsFilePath, "{\"theme\": \"dark\"}");

        Assert.Equal(ThemePreference.Dark, (await _store.LoadAsync()).Theme);
    }

    [Fact]
    public async Task UpdateAsync_KeepsOtherFields_WhenThemeChanges()
    {
        _ = await _store.UpdateAsync(s => s with { AutoCheckForUpdates = false, ReceiveBetaUpdates = true, LastRunVersion = "1.2.3" });

        _ = await _store.UpdateAsync(s => s with { Theme = ThemePreference.Light });

        var loaded = await _store.LoadAsync();
        Assert.Equal(ThemePreference.Light, loaded.Theme);
        Assert.False(loaded.AutoCheckForUpdates);
        Assert.True(loaded.ReceiveBetaUpdates);
        Assert.Equal("1.2.3", loaded.LastRunVersion);
    }

    [Fact]
    public async Task UpdateAsync_CreatesMissingFolder_WhenSettingsFileIsOverridden()
    {
        var custom = Path.Combine(_folder.Path, "not", "yet", "there", "custom.json");
        var paths = new AppDataPaths(Path.Combine(_folder.Path, "data"), custom);
        using var store = new JsonUserSettingsStore(paths, new CapturingLogger<JsonUserSettingsStore>());

        _ = await store.UpdateAsync(s => s with { Theme = ThemePreference.Dark });

        Assert.True(File.Exists(custom));
        Assert.False(Directory.Exists(paths.DataDirectory));
        Assert.Equal(ThemePreference.Dark, (await store.LoadAsync()).Theme);
    }

    [Fact]
    public async Task LoadAsync_ReturnsSystemTheme_WhenFileHasNoTheme()
    {
        _ = Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.SettingsFilePath, "{\"receiveBetaUpdates\": true}");

        Assert.Equal(ThemePreference.System, (await _store.LoadAsync()).Theme);
    }
}
