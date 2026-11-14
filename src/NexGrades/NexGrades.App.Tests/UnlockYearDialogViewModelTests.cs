using NexGrades.App.Core.YearLock;
using NexGrades.Domain.Services;

namespace NexGrades.App.Tests;

public class UnlockYearDialogViewModelTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 10, 30, 0);

    private static UnlockYearDialogViewModel Create(string reason = "correction", TestClock? clock = null) =>
        new("2025/26", clock ?? new TestClock(Now)) { Reason = reason };

    [Fact]
    public void IsValid_IsTrue_ForReasonAndDefaultDuration()
    {
        var viewModel = Create();

        Assert.True(viewModel.IsValid);
        Assert.Null(viewModel.ValidationMessage);
        Assert.False(viewModel.HasValidationMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_IsFalse_ForBlankReason(string reason)
    {
        var viewModel = Create(reason);

        Assert.False(viewModel.IsValid);
        Assert.NotNull(viewModel.ValidationMessage);
        Assert.Null(viewModel.TryCreateRequest());
    }

    [Fact]
    public void IsValid_IsFalse_ForReasonLongerThanMaxLength()
    {
        var viewModel = Create(new string('a', UnlockService.MaxReasonLength + 1));

        Assert.False(viewModel.IsValid);
        Assert.Null(viewModel.TryCreateRequest());
    }

    [Fact]
    public void IsValid_IsTrue_ForReasonOfExactlyMaxLength_AfterTrimming()
    {
        var viewModel = Create("  " + new string('a', UnlockService.MaxReasonLength) + "  ");

        Assert.True(viewModel.IsValid);
    }

    [Fact]
    public void IsValid_IsFalse_ForCustomDurationWithoutDate()
    {
        var viewModel = Create();
        viewModel.SelectedDuration = UnlockDurationOption.CustomDate;

        Assert.False(viewModel.IsValid);
        Assert.True(viewModel.IsCustomDate);
        Assert.Null(viewModel.TryCreateRequest());
    }

    [Fact]
    public void IsValid_IsFalse_ForCustomDateBeyondThirtyDays()
    {
        var viewModel = Create();
        viewModel.SelectedDuration = UnlockDurationOption.CustomDate;
        viewModel.CustomDate = Now.Date.AddDays(UnlockDuration.MaxCustomDays + 1);

        Assert.False(viewModel.IsValid);
        Assert.Null(viewModel.TryCreateRequest());
    }

    [Fact]
    public void IsValid_IsTrue_ForCustomDateExactlyThirtyDaysAhead()
    {
        var viewModel = Create();
        viewModel.SelectedDuration = UnlockDurationOption.CustomDate;
        viewModel.CustomDate = Now.Date.AddDays(UnlockDuration.MaxCustomDays);

        Assert.True(viewModel.IsValid);
    }

    [Fact]
    public void IsValid_IsFalse_ForCustomDateInThePast()
    {
        var viewModel = Create();
        viewModel.SelectedDuration = UnlockDurationOption.CustomDate;
        viewModel.CustomDate = Now.Date.AddDays(-1);

        Assert.False(viewModel.IsValid);
        Assert.Null(viewModel.TryCreateRequest());
    }

    [Fact]
    public void IsValid_IsTrue_ForCustomDateToday()
    {
        var viewModel = Create();
        viewModel.SelectedDuration = UnlockDurationOption.CustomDate;
        viewModel.CustomDate = Now.Date;

        Assert.True(viewModel.IsValid);
    }

    [Fact]
    public void Revalidate_ReturnsFalse_WhenTheClockMovedPastTheEnd()
    {
        var clock = new TestClock(Now);
        var viewModel = Create(clock: clock);
        viewModel.SelectedDuration = UnlockDurationOption.CustomDate;
        viewModel.CustomDate = Now.Date;
        Assert.True(viewModel.Revalidate());

        clock.Now = Now.Date.AddDays(1);

        Assert.False(viewModel.Revalidate());
    }

    [Fact]
    public void TryCreateRequest_ReturnsTrimmedReasonAndEnd_ForValidInput()
    {
        var viewModel = Create("  fix a typo  ");

        var request = viewModel.TryCreateRequest();

        Assert.NotNull(request);
        Assert.Equal("fix a typo", request.Reason);
        Assert.Equal(Now.AddHours(1), request.Until);
    }

    [Fact]
    public void CustomDateBounds_AreTodayAndThirtyDaysAhead()
    {
        var viewModel = Create();

        Assert.Equal(Now.Date, viewModel.CustomDateStart);
        Assert.Equal(Now.Date.AddDays(UnlockDuration.MaxCustomDays), viewModel.CustomDateEnd);
    }
}
