using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Core;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Tests;

/// <summary>
/// Tests the navigation guard of <see cref="PageNavigator"/>: cancel, prompt, re-issue exactly once, and no
/// leaking of navigation parameters. The fake navigation service behaves like the real one in the ways that
/// matter: it asks the navigator first (the <c>Navigating</c> event) and runs the view model callback later, not
/// inside the navigate call.
/// </summary>
public class PageNavigatorGuardTests
{
    private readonly List<Action> _cleanups = [];
    private readonly List<object?> _received = [];
    private readonly PageNavigator _navigator;
    private readonly ScriptedNavigation _navigation;
    private readonly INavigationService _service;

    public PageNavigatorGuardTests()
    {
        _navigator = new PageNavigator(
            ScriptedNavigation.Create(() => new TargetViewModel(_received), out _navigation),
            NullLogger<PageNavigator>.Instance,
            _cleanups.Add);
        _service = _navigation.Service;
        _navigation.Navigator = _navigator;
    }

    [Fact]
    public async Task NavigateWithHierarchy_ReissuesOnce_AfterUserDiscardsChanges()
    {
        var current = new GuardedViewModel();
        _navigator.SetCurrent(current);
        var parameter = new object();

        Assert.False(_navigator.NavigateWithHierarchy<TargetPage>(parameter));
        Assert.Equal(0, _navigation.Navigations);

        current.Gate.SetResult(true);
        await WaitUntilAsync(() => _navigation.Navigations == 1);
        await SettleAsync();

        Assert.Equal(1, _navigation.Navigations);
        Assert.Equal(1, current.CanLeaveCalls);

        await _navigation.DrainAsync();
        Assert.Single(_received);
        Assert.Same(parameter, _received[0]);
    }

    [Fact]
    public async Task NavigateWithHierarchy_StaysAndLeavesNoParameter_WhenUserKeepsEditing()
    {
        var current = new GuardedViewModel();
        _navigator.SetCurrent(current);

        Assert.False(_navigator.NavigateWithHierarchy<TargetPage>(new object()));
        current.Gate.SetResult(false);
        await SettleAsync();

        Assert.Equal(0, _navigation.Navigations);

        _navigator.SetCurrent(null);
        Assert.True(_navigator.NavigateWithHierarchy<TargetPage>());
        await _navigation.DrainAsync();

        Assert.Single(_received);
        Assert.Null(_received[0]);
    }

    [Fact]
    public async Task NavigateWithHierarchy_ShowsOnePrompt_WhenClickedTwiceWhilePromptIsOpen()
    {
        var current = new GuardedViewModel();
        _navigator.SetCurrent(current);

        Assert.False(_navigator.NavigateWithHierarchy<TargetPage>(new object()));
        Assert.False(_navigator.NavigateWithHierarchy<TargetPage>(new object()));
        Assert.Equal(1, current.CanLeaveCalls);

        current.Gate.SetResult(true);
        await WaitUntilAsync(() => _navigation.Navigations >= 1);
        await SettleAsync();

        Assert.Equal(1, _navigation.Navigations);
        Assert.Equal(1, current.CanLeaveCalls);
    }

    [Fact]
    public void NavigateWithHierarchy_IsNotCancelled_WhenHookCompletesSynchronouslyWithTrue()
    {
        _navigator.SetCurrent(new PlainViewModel());

        Assert.True(_navigator.NavigateWithHierarchy<TargetPage>());
        Assert.Equal(1, _navigation.Navigations);
    }

    [Fact]
    public void NavigateWithHierarchy_IsCancelled_WhenHookCompletesSynchronouslyWithFalse()
    {
        var current = new GuardedViewModel { Behavior = () => Task.FromResult(false) };
        _navigator.SetCurrent(current);

        Assert.False(_navigator.NavigateWithHierarchy<TargetPage>());
        Assert.Equal(0, _navigation.Navigations);
    }

    [Fact]
    public void NavigateWithHierarchy_IsNotCancelled_WhenNoCurrentViewModel()
    {
        Assert.True(_navigator.NavigateWithHierarchy<TargetPage>());
        Assert.Equal(1, _navigation.Navigations);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NavigateWithHierarchy_DropsParameter_WhenCallbackNeverRuns(bool throughGuard)
    {
        if (throughGuard)
        {
            var current = new GuardedViewModel();
            _navigator.SetCurrent(current);
            Assert.False(_navigator.NavigateWithHierarchy<TargetPage>(new object()));
            _navigation.NeverDrain = true;
            current.Gate.SetResult(true);
            await WaitUntilAsync(() => _navigation.Navigations == 1);
        }
        else
        {
            _navigation.NeverDrain = true;
            Assert.True(_navigator.NavigateWithHierarchy<TargetPage>(new object()));
        }

        await _navigation.DrainAsync();
        Assert.Empty(_received);
        var cleanup = Assert.Single(_cleanups);
        cleanup();

        _navigator.SetCurrent(null);
        _navigation.NeverDrain = false;
        Assert.True(_navigator.NavigateWithHierarchy<TargetPage>());
        await _navigation.DrainAsync();

        Assert.Single(_received);
        Assert.Null(_received[0]);
    }

    [Fact]
    public async Task NavigateWithHierarchy_StaysAndResetsGuard_WhenHookThrows()
    {
        var current = new GuardedViewModel();
        _navigator.SetCurrent(current);

        Assert.False(_navigator.NavigateWithHierarchy<TargetPage>());
        current.Gate.SetException(new InvalidOperationException("boom"));
        await SettleAsync();
        Assert.Equal(0, _navigation.Navigations);

        current.Behavior = () => Task.FromResult(false);
        Assert.False(_navigator.NavigateWithHierarchy<TargetPage>());

        Assert.Equal(2, current.CanLeaveCalls);
    }

    [Fact]
    public async Task NavigateWithHierarchy_PromptsAgain_AfterPreviousGuardCompleted()
    {
        var current = new GuardedViewModel();
        _navigator.SetCurrent(current);
        Assert.False(_navigator.NavigateWithHierarchy<TargetPage>());
        current.Gate.SetResult(true);
        await WaitUntilAsync(() => _navigation.Navigations == 1);
        await SettleAsync();

        current.Gate = new TaskCompletionSource<bool>();
        Assert.False(_navigator.NavigateWithHierarchy<TargetPage>());
        current.Gate.SetResult(false);
        await SettleAsync();

        Assert.Equal(2, current.CanLeaveCalls);
        Assert.Equal(1, _navigation.Navigations);
    }

    [Fact]
    public async Task ShouldCancelNavigating_UsesFallback_WhenNavigationDidNotComeFromNavigator()
    {
        var current = new GuardedViewModel();
        _navigator.SetCurrent(current);
        var fallbackCalls = 0;

        Assert.True(_navigator.ShouldCancelNavigating(() =>
        {
            fallbackCalls++;

            return true;
        }));
        Assert.Equal(0, fallbackCalls);

        current.Gate.SetResult(true);
        await WaitUntilAsync(() => fallbackCalls == 1);
        await SettleAsync();

        Assert.Equal(1, fallbackCalls);
        Assert.Equal(1, current.CanLeaveCalls);
    }

    [Fact]
    public async Task TryGuardBack_GoesBackOnce_AfterUserDiscardsChanges()
    {
        var current = new GuardedViewModel();
        _navigator.SetCurrent(current);

        Assert.False(_service.GoBack());
        Assert.Equal(0, _navigation.BackCalls);

        current.Gate.SetResult(true);
        await WaitUntilAsync(() => _navigation.BackCalls == 1);
        await SettleAsync();

        Assert.Equal(1, _navigation.BackCalls);
        Assert.Equal(1, current.CanLeaveCalls);
    }

    [Fact]
    public async Task TryGuardBack_ReturnsTrueWhilePending_AndSwallowsSecondClick()
    {
        var current = new GuardedViewModel();
        _navigator.SetCurrent(current);
        var backCalls = 0;

        Assert.True(_navigator.TryGuardBack(() =>
        {
            backCalls++;

            return true;
        }));
        Assert.True(_navigator.TryGuardBack(() =>
        {
            backCalls++;

            return true;
        }));
        Assert.Equal(1, current.CanLeaveCalls);

        current.Gate.SetResult(true);
        await WaitUntilAsync(() => backCalls >= 1);
        await SettleAsync();

        Assert.Equal(1, backCalls);
    }

    [Fact]
    public async Task TryGuardBack_NeverGoesBack_WhenUserKeepsEditing()
    {
        var current = new GuardedViewModel();
        _navigator.SetCurrent(current);

        Assert.False(_service.GoBack());
        current.Gate.SetResult(false);
        await SettleAsync();

        Assert.Equal(0, _navigation.BackCalls);
    }

    [Fact]
    public void TryGuardBack_ReturnsFalse_WhenNothingToAsk()
    {
        Assert.False(_navigator.TryGuardBack(() => true));

        _navigator.SetCurrent(new PlainViewModel());
        Assert.False(_navigator.TryGuardBack(() => true));
    }

    [Fact]
    public async Task NavigateWithHierarchy_ClearsBypass_WhenReissueThrows()
    {
        var current = new GuardedViewModel();
        _navigator.SetCurrent(current);
        _navigation.ThrowOnNavigate = true;

        Assert.False(_navigator.NavigateWithHierarchy<TargetPage>(new object()));
        current.Gate.SetResult(true);
        await SettleAsync();

        _navigation.ThrowOnNavigate = false;
        current.Gate = new TaskCompletionSource<bool>();
        Assert.False(_navigator.NavigateWithHierarchy<TargetPage>());

        Assert.Equal(2, current.CanLeaveCalls);
        Assert.Equal(0, _navigation.Navigations);
    }

    [Fact]
    public void Discard_RemovesStoredParameter_AndIgnoresMissingOne()
    {
        NavigationParameters.Set(typeof(DiscardMarker), new object());

        NavigationParameters.Discard(typeof(DiscardMarker));
        NavigationParameters.Discard(typeof(DiscardMarker));

        Assert.Null(NavigationParameters.Take(typeof(DiscardMarker)));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), "Timed out waiting for the condition.");
            await Task.Delay(1);
        }
    }

    /// <summary>Lets continuations that are already queued run, so "nothing happened" can be asserted.</summary>
    private static async Task SettleAsync()
    {
        for (var i = 0; i < 20; i++)
        {
            await Task.Yield();
        }
    }

    private sealed class DiscardMarker;

    /// <summary>Stands in for the page being opened.</summary>
    public sealed class TargetPage : INavigableView<TargetViewModel>
    {
        public TargetViewModel ViewModel { get; } = null!;
    }

    /// <summary>The view model of the page being opened; records the parameter it receives.</summary>
    public sealed class TargetViewModel(ICollection<object?> received) : ViewModel
    {
        protected override Task OnNavigatedToAsync(object? parameter)
        {
            received.Add(parameter);

            return Task.CompletedTask;
        }
    }

    /// <summary>A page with the default hook, which always allows leaving.</summary>
    public sealed class PlainViewModel : ViewModel;

    /// <summary>A page whose <see cref="CanLeaveAsync"/> waits for the test and counts its calls.</summary>
    public sealed class GuardedViewModel : ViewModel
    {
        public int CanLeaveCalls { get; private set; }

        public TaskCompletionSource<bool> Gate { get; set; } = new();

        public Func<Task<bool>>? Behavior { get; set; }

        public override Task<bool> CanLeaveAsync(CancellationToken cancellationToken = default)
        {
            CanLeaveCalls++;

            return Behavior?.Invoke() ?? Gate.Task;
        }
    }

    /// <summary>
    /// Fake <see cref="INavigationService"/>: navigating first asks the navigator whether to cancel (as the
    /// Navigating event does), and the view model callback is queued instead of run inside the call.
    /// </summary>
    public class ScriptedNavigation : DispatchProxy
    {
        private readonly Queue<Func<Task>> _pending = new();
        private Func<ViewModel> _createViewModel = null!;

        public INavigationService Service { get; private set; } = null!;

        public PageNavigator Navigator { get; set; } = null!;

        public int Navigations { get; private set; }

        public int BackCalls { get; private set; }

        public bool NeverDrain { get; set; }

        public bool ThrowOnNavigate { get; set; }

        public static INavigationService Create(Func<ViewModel> createViewModel, out ScriptedNavigation fake)
        {
            var proxy = Create<INavigationService, ScriptedNavigation>();
            fake = (ScriptedNavigation)(object)proxy;
            fake._createViewModel = createViewModel;
            fake.Service = proxy;

            return proxy;
        }

        /// <summary>Runs the queued navigated-to callbacks, like the UI thread would after the navigation.</summary>
        public async Task DrainAsync()
        {
            while (_pending.Count > 0)
            {
                await _pending.Dequeue()();
            }
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
            {
                return null;
            }

            if (targetMethod.Name == nameof(INavigationService.GoBack))
            {
                return GoBack();
            }

            if (args is null || !args.OfType<Type>().Any())
            {
                return targetMethod.ReturnType == typeof(bool) ? true : null;
            }

            if (Navigator.ShouldCancelNavigating(() => (bool)Invoke(targetMethod, args)!))
            {
                return false;
            }

            if (ThrowOnNavigate)
            {
                throw new InvalidOperationException("Navigation failed.");
            }

            Navigations++;
            if (!NeverDrain)
            {
                var viewModel = _createViewModel();
                _pending.Enqueue(() => viewModel.OnNavigatedToAsync());
            }

            return targetMethod.ReturnType == typeof(bool) ? true : null;
        }

        private bool GoBack()
        {
            if (Navigator.TryGuardBack(PerformBack))
            {
                return false;
            }

            return PerformBack();
        }

        private bool PerformBack()
        {
            if (Navigator.ShouldCancelNavigating(static () => false))
            {
                return false;
            }

            BackCalls++;

            return true;
        }
    }
}
