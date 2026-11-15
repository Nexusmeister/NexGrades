using System.IO;
using NexGrades.App.Features.Settings;
using NexGrades.App.Infrastructure;
using NexGrades.App.Theming;

namespace NexGrades.App.Tests;

public sealed class AppearanceSettingsViewModelTests
{
    private readonly FakeUserSettingsStore _store = new();
    private readonly FakeThemeApplier _applier = new();

    private (AppearanceSettingsViewModel ViewModel, FakeSnackbar Snackbar, AppThemeService Service) Create()
    {
        var service = new AppThemeService(_store, _applier, new CapturingLogger<AppThemeService>());
        var (snackbar, fake) = FakeSnackbar.Create();
        return (new AppearanceSettingsViewModel(service, snackbar), fake, service);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task LoadAsync_ShowsCurrentThemeWithoutWritingBack()
    {
        _store.Current = new UserSettings { Theme = ThemePreference.Dark };
        var (viewModel, snackbar, service) = Create();
        await service.InitializeAsync();
        _applier.Applied.Clear();

        await viewModel.LoadAsync();
        await Task.Delay(50);

        Assert.Equal(ThemePreference.Dark, viewModel.SelectedTheme);
        Assert.Equal(0, _store.UpdateCalls);
        Assert.Empty(_applier.Applied);
        Assert.Empty(snackbar.Titles);
    }

    [Fact]
    public void Options_ListSystemLightDark_InOrder()
    {
        var (viewModel, _, _) = Create();

        Assert.Equal([ThemePreference.System, ThemePreference.Light, ThemePreference.Dark], viewModel.Options.Select(o => o.Value));
    }

    [Fact]
    public async Task SelectedTheme_AppliesAndSavesTheme_WhenChanged()
    {
        var (viewModel, snackbar, _) = Create();

        viewModel.SelectedTheme = ThemePreference.Light;
        await WaitUntilAsync(() => _store.UpdateCalls == 1);

        Assert.Equal([ThemePreference.Light], _applier.Applied);
        Assert.Equal(ThemePreference.Light, _store.Current.Theme);
        Assert.Empty(snackbar.Titles);
    }

    [Fact]
    public async Task SelectedTheme_ShowsSnackbar_WhenSaveFails()
    {
        _store.UpdateFailure = new IOException("disk full");
        await StaTestRunner.RunAsync(async () =>
        {
            var (viewModel, snackbar, _) = Create();

            viewModel.SelectedTheme = ThemePreference.Dark;
            await WaitUntilAsync(() => snackbar.Titles.Count == 1);

            Assert.Equal([ThemePreference.Dark], _applier.Applied);
            Assert.Equal("Error", snackbar.Titles[0]);
        });
    }
}
