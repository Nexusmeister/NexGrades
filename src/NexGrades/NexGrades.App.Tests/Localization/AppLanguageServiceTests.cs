using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;
using NexGrades.App.Infrastructure;
using NexGrades.App.Localization;
using NexGrades.App.Tests.TestSupport;

namespace NexGrades.App.Tests.Localization;

public sealed class AppLanguageServiceTests
{
    private static readonly CultureInfo _windowsUi = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo _windowsFormat = CultureInfo.GetCultureInfo("en-US");

    private readonly FakeUserSettingsStore _store = new();
    private readonly CapturingLogger<AppLanguageService> _logger = new();

    private AppLanguageService Create() => new(_store, _logger);

    [Fact]
    public async Task InitializeAsync_LoadsSavedLanguage_AndResolvesCultures()
    {
        _store.Current = new UserSettings { Language = LanguagePreference.German };
        var service = Create();

        await service.InitializeAsync(_windowsUi, _windowsFormat);

        Assert.Equal(LanguagePreference.German, service.Current);
        Assert.Equal(LanguagePreference.German, service.Saved);
        Assert.Equal("de", service.Cultures.Ui.Name);
        Assert.Equal("de-DE", service.Cultures.Format.Name);
        Assert.False(service.IsRestartPending);
    }

    [Fact]
    public async Task InitializeAsync_UsesSystem_WhenSettingsHaveNoLanguage()
    {
        var service = Create();

        await service.InitializeAsync(_windowsUi, _windowsFormat);

        Assert.Equal(LanguagePreference.System, service.Current);
        Assert.Equal("en-US", service.Cultures.Ui.Name);
    }

    [Fact]
    public async Task InitializeAsync_UsesSystemAndLogsWarning_WhenStoreThrows()
    {
        _store.LoadFailure = new IOException("locked");
        var service = Create();

        await service.InitializeAsync(_windowsUi, _windowsFormat);

        Assert.Equal(LanguagePreference.System, service.Current);
        Assert.Equal("en-US", service.Cultures.Ui.Name);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task InitializeAsync_LogsOnlyTheExceptionType_WhenStoreThrows()
    {
        _store.LoadFailure = new IOException("secret-Zebra42");
        var service = Create();

        await service.InitializeAsync(_windowsUi, _windowsFormat);

        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("Zebra42", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InitializeAsync_UsesSystem_WhenSavedValueIsUndefined()
    {
        _store.Current = new UserSettings { Language = (LanguagePreference)42 };
        var service = Create();

        await service.InitializeAsync(_windowsUi, _windowsFormat);

        Assert.Equal(LanguagePreference.System, service.Current);
        Assert.Equal(LanguagePreference.System, service.Saved);
    }

    [Fact]
    public async Task InitializeAsync_UsesSystem_WhenSettingsFileIsMissing()
    {
        using var folder = new TempFolder();
        var paths = new NexGrades.App.Storage.AppDataPaths(Path.Combine(folder.Path, "data"));
        using var store = new JsonUserSettingsStore(paths, new CapturingLogger<JsonUserSettingsStore>());
        var service = new AppLanguageService(store, _logger);

        await service.InitializeAsync(_windowsUi, _windowsFormat);

        Assert.Equal(LanguagePreference.System, service.Current);
    }

    [Fact]
    public async Task SetAsync_SavesPreference_WithoutChangingCurrent()
    {
        var service = Create();
        await service.InitializeAsync(_windowsUi, _windowsFormat);

        var saved = await service.SetAsync(LanguagePreference.German);

        Assert.True(saved);
        Assert.Equal(LanguagePreference.German, _store.Current.Language);
        Assert.Equal(LanguagePreference.German, service.Saved);
        Assert.Equal(LanguagePreference.System, service.Current);
        Assert.Equal("en-US", service.Cultures.Ui.Name);
    }

    [Fact]
    public async Task SetAsync_KeepsOtherSettings()
    {
        _store.Current = new UserSettings { ReceiveBetaUpdates = true, LastRunVersion = "1.2.3" };
        var service = Create();

        _ = await service.SetAsync(LanguagePreference.English);

        Assert.True(_store.Current.ReceiveBetaUpdates);
        Assert.Equal("1.2.3", _store.Current.LastRunVersion);
    }

    [Fact]
    public async Task IsRestartPending_IsTrue_AfterChangingLanguage()
    {
        var service = Create();
        await service.InitializeAsync(_windowsUi, _windowsFormat);

        _ = await service.SetAsync(LanguagePreference.German);

        Assert.True(service.IsRestartPending);
    }

    [Fact]
    public async Task IsRestartPending_IsFalse_AfterChangingBackToCurrent()
    {
        var service = Create();
        await service.InitializeAsync(_windowsUi, _windowsFormat);
        _ = await service.SetAsync(LanguagePreference.German);

        _ = await service.SetAsync(LanguagePreference.System);

        Assert.False(service.IsRestartPending);
    }

    [Fact]
    public async Task SetAsync_ReturnsFalseAndKeepsSaved_WhenSaveFails()
    {
        var service = Create();
        await service.InitializeAsync(_windowsUi, _windowsFormat);
        _store.UpdateFailure = new IOException("disk full");

        var saved = await service.SetAsync(LanguagePreference.German);

        Assert.False(saved);
        Assert.Equal(LanguagePreference.System, service.Saved);
        Assert.False(service.IsRestartPending);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task SetAsync_Propagates_WhenCancelled()
    {
        var service = Create();
        _store.UpdateFailure = new OperationCanceledException();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => service.SetAsync(LanguagePreference.German));
    }
}
