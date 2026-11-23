using Microsoft.Extensions.Logging;
using NexGrades.App.Core;
using NexGrades.App.Tests.TestSupport;

namespace NexGrades.App.Tests.Core;

/// <summary>Tests <see cref="PageNavigator.ConfirmLeaveAsync"/>, the leave check for actions that are not a navigation.</summary>
public class PageNavigatorConfirmLeaveTests
{
    private readonly CapturingLogger<PageNavigator> _logger = new();
    private readonly PageNavigator _navigator;

    public PageNavigatorConfirmLeaveTests()
    {
        _navigator = new PageNavigator(FakeNavigationService.Create(), _logger, _ => { });
    }

    [Fact]
    public async Task ConfirmLeaveAsync_ReturnsTrue_WhenThereIsNoCurrentPage()
    {
        _navigator.SetCurrent(null);

        Assert.True(await _navigator.ConfirmLeaveAsync());
    }

    [Fact]
    public async Task ConfirmLeaveAsync_ReturnsTrue_WhenThePageAllowsLeaving()
    {
        _navigator.SetCurrent(new PageNavigatorGuardTests.PlainViewModel());

        Assert.True(await _navigator.ConfirmLeaveAsync());
    }

    [Fact]
    public async Task ConfirmLeaveAsync_ReturnsTrue_WhenTheUserDiscardsChanges()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel();
        _navigator.SetCurrent(current);
        current.Gate.SetResult(true);

        Assert.True(await _navigator.ConfirmLeaveAsync());
        Assert.Equal(1, current.CanLeaveCalls);
    }

    [Fact]
    public async Task ConfirmLeaveAsync_ReturnsFalse_WhenThePageRefusesToBeLeft()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel();
        _navigator.SetCurrent(current);
        current.Gate.SetResult(false);

        Assert.False(await _navigator.ConfirmLeaveAsync());
        Assert.Equal(1, current.CanLeaveCalls);
    }

    [Fact]
    public async Task ConfirmLeaveAsync_ReturnsFalseAndLogs_WhenThePageCheckThrows()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel();
        _navigator.SetCurrent(current);
        current.Gate.SetException(new InvalidOperationException("secret-Zebra42"));

        Assert.False(await _navigator.ConfirmLeaveAsync());

        var entry = Assert.Single(_logger.Entries, e => e.Level >= LogLevel.Warning);
        Assert.DoesNotContain("Zebra42", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmLeaveAsync_CanBeAskedAgain_AfterTheCheckThrew()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel();
        _navigator.SetCurrent(current);
        current.Behavior = () => Task.FromException<bool>(new InvalidOperationException("boom"));
        Assert.False(await _navigator.ConfirmLeaveAsync());

        current.Behavior = () => Task.FromResult(true);

        Assert.True(await _navigator.ConfirmLeaveAsync());
        Assert.Equal(2, current.CanLeaveCalls);
    }

    [Fact]
    public async Task ConfirmLeaveAsync_ReturnsFalseWithoutAskingAgain_WhileALeavePromptIsOpen()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel();
        _navigator.SetCurrent(current);

        var first = _navigator.ConfirmLeaveAsync();
        var second = await _navigator.ConfirmLeaveAsync();

        Assert.False(second);
        Assert.Equal(1, current.CanLeaveCalls);

        current.Gate.SetResult(true);
        Assert.True(await first);
    }

    [Fact]
    public async Task ConfirmLeaveAsync_PromptsAgain_AfterAnEarlierPromptWasAnswered()
    {
        var current = new PageNavigatorGuardTests.GuardedViewModel();
        _navigator.SetCurrent(current);
        current.Gate.SetResult(false);
        Assert.False(await _navigator.ConfirmLeaveAsync());

        current.Gate = new TaskCompletionSource<bool>();
        current.Gate.SetResult(true);

        Assert.True(await _navigator.ConfirmLeaveAsync());
        Assert.Equal(2, current.CanLeaveCalls);
    }
}
