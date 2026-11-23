using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using NexGrades.App.Infrastructure;
using NexGrades.App.Storage;
using NexGrades.App.Tests.TestSupport;
using NexGrades.App.Theming;

namespace NexGrades.App.Tests.Theming;

public sealed class AppThemeServiceTests : IDisposable
{
    private readonly FakeUserSettingsStore _store = new();
    private readonly FakeThemeApplier _applier = new();
    private readonly CapturingLogger<AppThemeService> _logger = new();
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private AppThemeService CreateService() => new(_store, _applier, _logger);

    [Theory]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    public async Task InitializeAsync_AppliesStoredTheme_WhenSettingsLoad(ThemePreference stored)
    {
        _store.Current = new UserSettings { Theme = stored };
        var service = CreateService();

        await service.InitializeAsync();

        Assert.Equal([stored], _applier.Applied);
        Assert.Equal(stored, service.Current);
    }

    [Fact]
    public async Task InitializeAsync_FallsBackToSystem_WhenStoreThrows()
    {
        _store.LoadFailure = new IOException("disk gone");
        var service = CreateService();

        await service.InitializeAsync();

        Assert.Equal([ThemePreference.System], _applier.Applied);
        Assert.Equal(ThemePreference.System, service.Current);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task InitializeAsync_FallsBackToSystem_WhenValueIsUndefined()
    {
        _store.Current = new UserSettings { Theme = (ThemePreference)99 };
        var service = CreateService();

        await service.InitializeAsync();

        Assert.Equal([ThemePreference.System], _applier.Applied);
        Assert.Equal(ThemePreference.System, service.Current);
    }

    [Fact]
    public async Task InitializeAsync_FallsBackToSystem_WhenFileHoldsNumericValueOutsideTheEnum()
    {
        var paths = new AppDataPaths(Path.Combine(_folder.Path, "data"));
        using var store = new JsonUserSettingsStore(paths, new CapturingLogger<JsonUserSettingsStore>());
        _ = Directory.CreateDirectory(paths.DataDirectory);
        await File.WriteAllTextAsync(paths.SettingsFilePath, "{\"theme\": 99}");
        var service = new AppThemeService(store, _applier, _logger);

        await service.InitializeAsync();

        Assert.Equal([ThemePreference.System], _applier.Applied);
    }

    [Fact]
    public async Task SetAsync_AppliesAndPersists_WhenSaveWorks()
    {
        var service = CreateService();

        var saved = await service.SetAsync(ThemePreference.Dark);

        Assert.True(saved);
        Assert.Equal([ThemePreference.Dark], _applier.Applied);
        Assert.Equal(ThemePreference.Dark, service.Current);
        Assert.Equal(ThemePreference.Dark, _store.Current.Theme);
    }

    [Fact]
    public async Task SetAsync_AppliesAndReturnsFalse_WhenSaveFails()
    {
        var path = Path.Combine(_folder.Path, "secret-folder", "settings.json");
        _store.UpdateFailure = new IOException($"cannot write {path}");
        var service = CreateService();

        var saved = await service.SetAsync(ThemePreference.Light);

        Assert.False(saved);
        Assert.Equal([ThemePreference.Light], _applier.Applied);
        Assert.Equal(ThemePreference.Light, service.Current);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("secret-folder", StringComparison.Ordinal) || e.Message.Contains(_folder.Path, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SetAsync_AppliesAndReturnsFalse_WhenAccessIsDenied()
    {
        _store.UpdateFailure = new UnauthorizedAccessException();
        var service = CreateService();

        Assert.False(await service.SetAsync(ThemePreference.Dark));
        Assert.Equal([ThemePreference.Dark], _applier.Applied);
    }

    [Fact]
    public async Task AttachWindow_DelegatesToApplier()
    {
        await StaTestRunner.RunAsync(() =>
        {
            var window = new Window();
            var service = CreateService();

            service.AttachWindow(window);

            Assert.Same(window, Assert.Single(_applier.AttachedWindows));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task SetAsync_AppliesAndReturnsFalse_WhenStoreThrowsNonIoException()
    {
        _store.UpdateFailure = new InvalidOperationException("secret-folder");
        var service = CreateService();

        var saved = await service.SetAsync(ThemePreference.Dark);

        Assert.False(saved);
        Assert.Equal([ThemePreference.Dark], _applier.Applied);
        Assert.Equal(ThemePreference.Dark, service.Current);
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("secret-folder", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetAsync_Throws_WhenSaveIsCancelled()
    {
        _store.UpdateFailure = new OperationCanceledException();
        var service = CreateService();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => service.SetAsync(ThemePreference.Dark));
    }
}
