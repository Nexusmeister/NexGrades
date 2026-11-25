using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using NexGrades.App.Features.Settings;
using NexGrades.App.Infrastructure;
using NexGrades.App.Localization;
using NexGrades.App.Tests.TestSupport;

namespace NexGrades.App.Tests.Features.Settings;

[SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "Tests await the view model's own save chain and a gate they own and release themselves.")]
public sealed class LanguageSettingsViewModelTests
{
    private static readonly CultureInfo _windows = CultureInfo.GetCultureInfo("en-US");

    private readonly FakeUserSettingsStore _store = new();

    private async Task<(LanguageSettingsViewModel ViewModel, FakeSnackbar Snackbar, AppLanguageService Service)> CreateAsync()
    {
        var service = new AppLanguageService(_store, new CapturingLogger<AppLanguageService>());
        await service.InitializeAsync(_windows, _windows);
        var (snackbar, fake) = FakeSnackbar.Create();
        return (new LanguageSettingsViewModel(service, snackbar), fake, service);
    }

    [Fact]
    public async Task Options_ListSystemEnglishGerman_InOrder()
    {
        var (viewModel, _, _) = await CreateAsync();

        Assert.Equal([LanguagePreference.System, LanguagePreference.English, LanguagePreference.German], viewModel.Options.Select(o => o.Value));
    }

    [Fact]
    public async Task Options_ShowLanguageNamesInTheirOwnLanguage()
    {
        var (viewModel, _, _) = await CreateAsync();

        Assert.Equal("English", viewModel.Options.Single(o => o.Value == LanguagePreference.English).Label);
        Assert.Equal("Deutsch", viewModel.Options.Single(o => o.Value == LanguagePreference.German).Label);
    }

    [Fact]
    public async Task LoadAsync_ShowsSavedLanguageWithoutWriting()
    {
        _store.Current = new UserSettings { Language = LanguagePreference.German };
        var (viewModel, snackbar, _) = await CreateAsync();

        await viewModel.LoadAsync();
        await viewModel.PendingSave;

        Assert.Equal(LanguagePreference.German, viewModel.SelectedLanguage);
        Assert.False(viewModel.IsRestartPending);
        Assert.Equal(0, _store.UpdateCalls);
        Assert.Empty(snackbar.Titles);
    }

    [Fact]
    public async Task LoadAsync_ShowsRestartPending_WhenSavedDiffersFromCurrent()
    {
        var (viewModel, _, service) = await CreateAsync();
        _ = await service.SetAsync(LanguagePreference.German);
        var updatesBefore = _store.UpdateCalls;

        await viewModel.LoadAsync();
        await viewModel.PendingSave;

        Assert.Equal(LanguagePreference.German, viewModel.SelectedLanguage);
        Assert.True(viewModel.IsRestartPending);
        Assert.Equal(updatesBefore, _store.UpdateCalls);
    }

    [Fact]
    public async Task SelectedLanguage_SavesAndSetsRestartPending_WhenChanged()
    {
        var (viewModel, snackbar, _) = await CreateAsync();

        viewModel.SelectedLanguage = LanguagePreference.German;
        await viewModel.PendingSave;

        Assert.Equal(LanguagePreference.German, _store.Current.Language);
        Assert.True(viewModel.IsRestartPending);
        Assert.Empty(snackbar.Titles);
    }

    [Fact]
    public async Task SelectedLanguage_ClearsRestartPending_WhenChangedBackToCurrent()
    {
        var (viewModel, _, _) = await CreateAsync();
        viewModel.SelectedLanguage = LanguagePreference.German;
        await viewModel.PendingSave;

        viewModel.SelectedLanguage = LanguagePreference.System;
        await viewModel.PendingSave;

        Assert.False(viewModel.IsRestartPending);
        Assert.Equal(LanguagePreference.System, _store.Current.Language);
    }

    [Fact]
    public async Task SelectedLanguage_WritesInOrderAndEndsOnLastChoice_WhenSelectedTwiceWhileFirstWriteIsBlocked()
    {
        var store = new GatedSettingsStore();
        var service = new AppLanguageService(store, new CapturingLogger<AppLanguageService>());
        await service.InitializeAsync(_windows, _windows);
        var (snackbar, fake) = FakeSnackbar.Create();
        var viewModel = new LanguageSettingsViewModel(service, snackbar) { SelectedLanguage = LanguagePreference.English };
        for (var i = 0; i < 300 && store.UpdateCalls < 1; i++)
        {
            await Task.Delay(10);
        }

        viewModel.SelectedLanguage = LanguagePreference.German;
        await Task.Delay(50);

        Assert.Equal(1, store.UpdateCalls);
        Assert.Empty(store.Written);

        store.Gate.SetResult();
        await viewModel.PendingSave;

        Assert.Equal([LanguagePreference.English, LanguagePreference.German], store.Written);
        Assert.Equal(LanguagePreference.German, store.Current.Language);
        Assert.Empty(fake.Titles);
    }

    [Fact]
    public async Task SelectedLanguage_ShowsErrorSnackbar_WhenSaveFails()
    {
        _store.UpdateFailure = new IOException("disk full");
        await StaTestRunner.RunAsync(async () =>
        {
            var (viewModel, snackbar, _) = await CreateAsync();

            viewModel.SelectedLanguage = LanguagePreference.German;
            await viewModel.PendingSave;

            Assert.Equal(["Error"], snackbar.Titles);
            Assert.Equal(["The language could not be saved."], snackbar.Messages);
            Assert.False(viewModel.IsRestartPending);
            Assert.Equal(LanguagePreference.System, _store.Current.Language);
        });
    }

    [Fact]
    public async Task SelectedLanguage_ResetsToSavedWithoutWriting_WhenSaveFails()
    {
        _store.UpdateFailure = new IOException("disk full");
        await StaTestRunner.RunAsync(async () =>
        {
            var (viewModel, _, service) = await CreateAsync();
            var updatesBefore = _store.UpdateCalls;

            viewModel.SelectedLanguage = LanguagePreference.German;
            await viewModel.PendingSave;

            Assert.Equal(service.Saved, viewModel.SelectedLanguage);
            Assert.Equal(LanguagePreference.System, viewModel.SelectedLanguage);
            Assert.False(viewModel.IsRestartPending);
            Assert.Equal(updatesBefore + 1, _store.UpdateCalls);
        });
    }

    [Fact]
    public async Task SelectedLanguage_SavesLaterChoice_WhenEarlierSaveFailed()
    {
        _store.UpdateFailure = new IOException("disk full");
        await StaTestRunner.RunAsync(async () =>
        {
            var (viewModel, snackbar, _) = await CreateAsync();

            viewModel.SelectedLanguage = LanguagePreference.German;
            await viewModel.PendingSave;
            _store.UpdateFailure = null;
            viewModel.SelectedLanguage = LanguagePreference.English;
            await viewModel.PendingSave;

            Assert.Equal(["Error"], snackbar.Titles);
            Assert.Equal(LanguagePreference.English, _store.Current.Language);
            Assert.True(viewModel.IsRestartPending);
        });
    }

    /// <summary>A store whose first <c>UpdateAsync</c> waits for <see cref="Gate"/>; it records the language of each write in order.</summary>
    private sealed class GatedSettingsStore : IUserSettingsStore
    {
        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public UserSettings Current { get; private set; } = new();

        public int UpdateCalls { get; private set; }

        public List<LanguagePreference> Written { get; } = [];

        public Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);

        public async Task<UserSettings> UpdateAsync(Func<UserSettings, UserSettings> update, CancellationToken cancellationToken = default)
        {
            var call = ++UpdateCalls;
            if (call == 1)
            {
                await Gate.Task;
            }

            Current = update(Current);
            Written.Add(Current.Language);
            return Current;
        }
    }
}
