using System.IO;
using System.Text.Json;
using NexGrades.App.Infrastructure;
using NexGrades.App.Localization;
using NexGrades.App.Storage;
using NexGrades.App.Tests.TestSupport;

namespace NexGrades.App.Tests.Localization;

public sealed class LanguageSettingsStoreTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly AppDataPaths _paths;
    private readonly JsonUserSettingsStore _store;

    public LanguageSettingsStoreTests()
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
    public void UserSettings_DefaultsToSystemLanguage()
    {
        Assert.Equal(LanguagePreference.System, new UserSettings().Language);
    }

    [Fact]
    public async Task LoadAsync_ReturnsSystemLanguage_WhenFileHasNoLanguage()
    {
        _ = Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.SettingsFilePath, "{\"theme\": \"Dark\", \"receiveBetaUpdates\": true}");

        var settings = await _store.LoadAsync();

        Assert.Equal(LanguagePreference.System, settings.Language);
        Assert.True(settings.ReceiveBetaUpdates);
    }

    [Theory]
    [InlineData(LanguagePreference.English)]
    [InlineData(LanguagePreference.German)]
    public async Task LoadAsync_ReadsBackLanguage_AfterUpdateAsync(LanguagePreference language)
    {
        _ = await _store.UpdateAsync(s => s with { Language = language });

        using var fresh = new JsonUserSettingsStore(_paths, new CapturingLogger<JsonUserSettingsStore>());
        Assert.Equal(language, (await fresh.LoadAsync()).Language);
    }

    [Fact]
    public async Task UpdateAsync_StoresLanguageByName_InTheFile()
    {
        _ = await _store.UpdateAsync(s => s with { Language = LanguagePreference.German });

        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(_paths.SettingsFilePath));
        Assert.Equal("German", json.RootElement.GetProperty("language").GetString());
    }

    [Fact]
    public async Task LoadAsync_ReadsLowerCaseLanguageName()
    {
        _ = Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.SettingsFilePath, "{\"language\": \"german\"}");

        Assert.Equal(LanguagePreference.German, (await _store.LoadAsync()).Language);
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefaultsWithoutThrowing_WhenLanguageNameIsUnknown()
    {
        _ = Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.SettingsFilePath, "{\"language\": \"Klingon\"}");

        var settings = await _store.LoadAsync();

        Assert.Equal(new UserSettings(), settings);
    }

    [Fact]
    public async Task UpdateAsync_KeepsOtherFields_WhenLanguageChanges()
    {
        _ = await _store.UpdateAsync(s => s with { ReceiveBetaUpdates = true, LastRunVersion = "1.2.3" });

        _ = await _store.UpdateAsync(s => s with { Language = LanguagePreference.English });

        var loaded = await _store.LoadAsync();
        Assert.Equal(LanguagePreference.English, loaded.Language);
        Assert.True(loaded.ReceiveBetaUpdates);
        Assert.Equal("1.2.3", loaded.LastRunVersion);
    }
}
