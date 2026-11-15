using System.Diagnostics.CodeAnalysis;
using System.IO;
using NexGrades.App.Features.Settings;
using NexGrades.App.Infrastructure;
using NexGrades.App.Theming;

namespace NexGrades.App.Tests;

[SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "Tests await the view model's own save chain and a gate they own and release themselves.")]
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

    [Fact]
    public async Task SelectedTheme_WritesInOrderAndEndsOnLastChoice_WhenSelectedTwiceWhileFirstWriteIsBlocked()
    {
        var store = new GatedSettingsStore();
        var service = new AppThemeService(store, _applier, new CapturingLogger<AppThemeService>());
        var (snackbar, fake) = FakeSnackbar.Create();
        var viewModel = new AppearanceSettingsViewModel(service, snackbar) { SelectedTheme = ThemePreference.Light };
        await WaitUntilAsync(() => store.UpdateCalls == 1);
        viewModel.SelectedTheme = ThemePreference.Dark;
        await Task.Delay(50);

        Assert.Equal(1, store.UpdateCalls);
        Assert.Empty(store.Written);

        store.Gate.SetResult();
        await viewModel.PendingSave;

        Assert.Equal([ThemePreference.Light, ThemePreference.Dark], store.Written);
        Assert.Equal(ThemePreference.Dark, store.Current.Theme);
        Assert.Empty(fake.Titles);
    }

    [Fact]
    public async Task SelectedTheme_ShowsSnackbarAndStaysUsable_WhenStoreThrowsAnyException()
    {
        _store.UpdateFailure = new InvalidOperationException("not an IO problem");
        await StaTestRunner.RunAsync(async () =>
        {
            var (viewModel, snackbar, _) = Create();

            viewModel.SelectedTheme = ThemePreference.Dark;
            await viewModel.PendingSave;

            Assert.Equal(["Error"], snackbar.Titles);

            _store.UpdateFailure = null;
            viewModel.SelectedTheme = ThemePreference.Light;
            await viewModel.PendingSave;

            Assert.Equal(["Error"], snackbar.Titles);
            Assert.Equal(ThemePreference.Light, _store.Current.Theme);
            Assert.Equal([ThemePreference.Dark, ThemePreference.Light], _applier.Applied);
        });
    }

    [Fact]
    public async Task SelectedTheme_SavesLaterChoice_WhenEarlierSaveFailed()
    {
        _store.UpdateFailure = new IOException("disk full");
        await StaTestRunner.RunAsync(async () =>
        {
            var (viewModel, snackbar, _) = Create();

            viewModel.SelectedTheme = ThemePreference.Dark;
            await viewModel.PendingSave;
            _store.UpdateFailure = null;
            viewModel.SelectedTheme = ThemePreference.Light;
            await viewModel.PendingSave;

            Assert.Equal(["Error"], snackbar.Titles);
            Assert.Equal(ThemePreference.Light, _store.Current.Theme);
        });
    }

    [Fact]
    public async Task LoadAsync_WritesNothing_WhenStoredThemeDiffersFromSelection()
    {
        _store.Current = new UserSettings { Theme = ThemePreference.Light };
        var (viewModel, _, service) = Create();
        await service.InitializeAsync();
        _applier.Applied.Clear();

        await viewModel.LoadAsync();
        await viewModel.PendingSave;

        Assert.Equal(ThemePreference.Light, viewModel.SelectedTheme);
        Assert.Equal(0, _store.UpdateCalls);
        Assert.Empty(_applier.Applied);
    }

    /// <summary>A store whose first <c>UpdateAsync</c> waits for <see cref="Gate"/>; it records the theme of each write in order.</summary>
    private sealed class GatedSettingsStore : IUserSettingsStore
    {
        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public UserSettings Current { get; private set; } = new();

        public int UpdateCalls { get; private set; }

        public List<ThemePreference> Written { get; } = [];

        public Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);

        public async Task<UserSettings> UpdateAsync(Func<UserSettings, UserSettings> update, CancellationToken cancellationToken = default)
        {
            var call = ++UpdateCalls;
            if (call == 1)
            {
                await Gate.Task;
            }

            Current = update(Current);
            Written.Add(Current.Theme);
            return Current;
        }
    }
}
