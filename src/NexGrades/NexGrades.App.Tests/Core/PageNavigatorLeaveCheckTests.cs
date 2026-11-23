using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Core;
using NexGrades.App.Tests.TestSupport;
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Tests.Core;

/// <summary>
/// Tests the order and failure handling of the leave check in <see cref="PageNavigator"/>: the pending-input commit
/// runs before <see cref="ViewModel.CanLeaveAsync"/>, a throwing hook keeps the user on the page, the back button asks
/// once, and a confirmed exit is remembered.
/// </summary>
[Collection(NavigationParametersState.Name)]
public class PageNavigatorLeaveCheckTests
{
    private readonly CapturingLogger<PageNavigator> _logger = new();
    private readonly PageNavigator _navigator;

    public PageNavigatorLeaveCheckTests()
    {
        _navigator = new PageNavigator(FakeNavigationService.Create(), _logger, _ => { });
    }

    [Fact]
    public void TryGuardBack_AsksOnceAndGoesBack_WhenHookCompletesSynchronouslyWithTrue()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel { Behavior = () => Task.FromResult(true) };
        _navigator.SetCurrent(current);
        var backCalls = 0;

        Assert.True(_navigator.TryGuardBack(() =>
        {
            backCalls++;

            return true;
        }));

        Assert.Equal(1, current.CanLeaveCalls);
        Assert.Equal(1, backCalls);
    }

    [Fact]
    public async Task TryGuardBack_AsksOnceAndGoesBack_WhenHookCompletesAsynchronouslyWithTrue()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel();
        _navigator.SetCurrent(current);
        var backCalls = 0;

        Assert.True(_navigator.TryGuardBack(() =>
        {
            backCalls++;

            return true;
        }));
        Assert.Equal(0, backCalls);

        current.Gate.SetResult(true);
        await LockTestEnvironment.WaitUntilAsync(() => backCalls == 1);

        Assert.Equal(1, current.CanLeaveCalls);
        Assert.Equal(1, backCalls);
    }

    [Fact]
    public void TryGuardBack_StaysAndDoesNotGoBack_WhenHookCompletesSynchronouslyWithFalse()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel { Behavior = () => Task.FromResult(false) };
        _navigator.SetCurrent(current);
        var backCalls = 0;

        Assert.True(_navigator.TryGuardBack(() =>
        {
            backCalls++;

            return true;
        }));

        Assert.Equal(1, current.CanLeaveCalls);
        Assert.Equal(0, backCalls);
    }

    [Fact]
    public void TryGuardBack_StaysAndLogsWithoutMessage_WhenHookThrowsSynchronously()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel { Behavior = () => throw new InvalidOperationException("secret-Zebra42") };
        _navigator.SetCurrent(current);
        var backCalls = 0;

        Assert.True(_navigator.TryGuardBack(() =>
        {
            backCalls++;

            return true;
        }));

        Assert.Equal(0, backCalls);
        AssertLoggedWithoutMessage();
    }

    [Fact]
    public void ShouldCancelNavigating_Cancels_WhenHookThrowsSynchronously()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel { Behavior = () => throw new InvalidOperationException("secret-Zebra42") };
        _navigator.SetCurrent(current);

        Assert.True(_navigator.ShouldCancelNavigating(() => true));
        AssertLoggedWithoutMessage();
    }

    [Fact]
    public void ShouldCancelNavigating_Cancels_WhenCommitPendingInputThrows()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel
        {
            Behavior = () => Task.FromResult(true),
            CommitPendingInput = () => throw new InvalidOperationException("secret-Zebra42")
        };
        _navigator.SetCurrent(current);

        Assert.True(_navigator.ShouldCancelNavigating(() => true));
        Assert.Equal(0, current.CanLeaveCalls);
        AssertLoggedWithoutMessage();
    }

    [Fact]
    public void ShouldCancelNavigating_CancelsWithoutAskingPage_WhenCommitPendingInputReturnsFalse()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel
        {
            Behavior = () => Task.FromResult(true),
            CommitPendingInput = () => false
        };
        _navigator.SetCurrent(current);

        Assert.True(_navigator.ShouldCancelNavigating(() => true));
        Assert.Equal(0, current.CanLeaveCalls);
    }

    [Fact]
    public void TryGuardBack_DoesNotGoBackOrAskPage_WhenCommitPendingInputReturnsFalse()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel
        {
            Behavior = () => Task.FromResult(true),
            CommitPendingInput = () => false
        };
        _navigator.SetCurrent(current);
        var backCalls = 0;

        Assert.True(_navigator.TryGuardBack(() =>
        {
            backCalls++;

            return true;
        }));

        Assert.Equal(0, backCalls);
        Assert.Equal(0, current.CanLeaveCalls);
    }

    [Fact]
    public void ShouldCancelNavigating_RunsCommitBeforeCanLeave_WhenBothAllow()
    {
        var order = new List<string>();
        var current = new PageNavigatorGuardTests.GuardedViewModel
        {
            Behavior = () =>
                    {
                        order.Add("canLeave");

                        return Task.FromResult(true);
                    },
            CommitPendingInput = () =>
                {
                    order.Add("commit");

                    return true;
                }
        };
        _navigator.SetCurrent(current);

        Assert.False(_navigator.ShouldCancelNavigating(() => true));
        Assert.Equal(["commit", "canLeave"], order);
    }

    [Fact]
    public async Task ConfirmLeaveAsync_ReturnsFalseWithoutAskingPage_WhenCommitPendingInputReturnsFalse()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel
        {
            Behavior = () => Task.FromResult(true),
            CommitPendingInput = () => false
        };
        _navigator.SetCurrent(current);

        Assert.False(await _navigator.ConfirmLeaveAsync());
        Assert.Equal(0, current.CanLeaveCalls);
    }

    [Fact]
    public async Task ConfirmLeaveAsync_ReturnsFalseAndLogs_WhenCommitPendingInputThrows()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel
        {
            Behavior = () => Task.FromResult(true),
            CommitPendingInput = () => throw new InvalidOperationException("secret-Zebra42")
        };
        _navigator.SetCurrent(current);

        Assert.False(await _navigator.ConfirmLeaveAsync());
        AssertLoggedWithoutMessage();
    }

    [Fact]
    public async Task ConfirmExitAsync_SetsExitConfirmed_WhenUserAgrees()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel { Behavior = () => Task.FromResult(true) };
        _navigator.SetCurrent(current);

        Assert.True(await _navigator.ConfirmExitAsync());
        Assert.True(_navigator.ExitConfirmed);
    }

    [Fact]
    public async Task ConfirmExitAsync_DoesNotSetExitConfirmed_WhenUserStays()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel { Behavior = () => Task.FromResult(false) };
        _navigator.SetCurrent(current);

        Assert.False(await _navigator.ConfirmExitAsync());
        Assert.False(_navigator.ExitConfirmed);
    }

    [Fact]
    public async Task ConfirmExitAsync_PromptsAgain_AfterUserStayed()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel { Behavior = () => Task.FromResult(false) };
        _navigator.SetCurrent(current);
        Assert.False(await _navigator.ConfirmExitAsync());

        current.Behavior = () => Task.FromResult(true);

        Assert.True(await _navigator.ConfirmExitAsync());
        Assert.Equal(2, current.CanLeaveCalls);
    }

    [Fact]
    public async Task ConfirmExitAsync_DoesNotPromptAgain_WhenExitAlreadyConfirmed()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel { Behavior = () => Task.FromResult(true) };
        _navigator.SetCurrent(current);
        Assert.True(await _navigator.ConfirmExitAsync());

        Assert.True(await _navigator.ConfirmExitAsync());
        Assert.Equal(1, current.CanLeaveCalls);
    }

    [Fact]
    public async Task NavigateWithHierarchy_KeepsNewerParameter_WhenTwoNavigationsPrecedeTheCleanups()
    {
        var received = new List<object?>();
        var cleanups = new List<Action>();
        var navigator = new PageNavigator(
            PageNavigatorGuardTests.ScriptedNavigation.Create(() => new IsolatedViewModel(received), out var navigation),
            NullLogger<PageNavigator>.Instance,
            cleanups.Add);
        navigation.Navigator = navigator;
        var older = new object();
        var newer = new object();

        Assert.True(navigator.NavigateWithHierarchy<IsolatedPage>(older));
        Assert.True(navigator.NavigateWithHierarchy<IsolatedPage>(newer));
        Assert.Equal(2, cleanups.Count);

        cleanups[0]();
        await navigation.DrainAsync();

        Assert.Equal(2, received.Count);
        Assert.Same(newer, Assert.Single(received, r => r is not null));
    }

    private void AssertLoggedWithoutMessage()
    {
        var entry = Assert.Single(_logger.Entries, e => e.Level >= LogLevel.Warning);
        Assert.DoesNotContain("Zebra42", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A page and view model used by no other test, so the type-keyed hand-over of the parameter cannot be taken
    /// or replaced by a test running at the same time (#160).
    /// </summary>
    public sealed class IsolatedPage : INavigableView<IsolatedViewModel>
    {
        public IsolatedViewModel ViewModel { get; } = null!;
    }

    /// <summary>The view model of <see cref="IsolatedPage"/>; records the parameter it receives.</summary>
    public sealed class IsolatedViewModel(ICollection<object?> received) : ViewModel
    {
        protected override Task OnNavigatedToAsync(object? parameter)
        {
            received.Add(parameter);

            return Task.CompletedTask;
        }
    }
}
