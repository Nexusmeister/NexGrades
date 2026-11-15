using NexGrades.App.Core;

namespace NexGrades.App.Tests;

/// <summary>Tests the versioned one-shot hand-over of <see cref="NavigationParameters"/>.</summary>
[Collection(NavigationParametersState.Name)]
public class NavigationParametersTests
{
    [Fact]
    public void Set_ReturnsIncreasingVersions_ForRepeatedCalls()
    {
        var first = NavigationParameters.Set(typeof(VersionMarker), new object());
        var second = NavigationParameters.Set(typeof(VersionMarker), new object());

        Assert.True(second > first);
        NavigationParameters.Discard(typeof(VersionMarker));
    }

    [Fact]
    public void Discard_RemovesValue_WhenVersionIsStillCurrent()
    {
        var version = NavigationParameters.Set(typeof(CurrentMarker), new object());

        NavigationParameters.Discard(typeof(CurrentMarker), version);

        Assert.Null(NavigationParameters.Take(typeof(CurrentMarker)));
    }

    [Fact]
    public void Discard_LeavesNewerValue_WhenVersionIsOlder()
    {
        var older = NavigationParameters.Set(typeof(NewerMarker), new object());
        var newer = new object();
        NavigationParameters.Set(typeof(NewerMarker), newer);

        NavigationParameters.Discard(typeof(NewerMarker), older);

        Assert.Same(newer, NavigationParameters.Take(typeof(NewerMarker)));
    }

    [Fact]
    public void Discard_DoesNothing_WhenNoValueIsStored()
    {
        NavigationParameters.Discard(typeof(EmptyMarker), 1);

        Assert.Null(NavigationParameters.Take(typeof(EmptyMarker)));
    }

    [Fact]
    public void Take_ReturnsValueOnce_ThenNull()
    {
        var value = new object();
        NavigationParameters.Set(typeof(TakeMarker), value);

        Assert.Same(value, NavigationParameters.Take(typeof(TakeMarker)));
        Assert.Null(NavigationParameters.Take(typeof(TakeMarker)));
    }

    private sealed class VersionMarker;

    private sealed class CurrentMarker;

    private sealed class NewerMarker;

    private sealed class EmptyMarker;

    private sealed class TakeMarker;
}
