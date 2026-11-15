using NexGrades.App.Features.Students;

namespace NexGrades.App.Tests;

public class ExportNotesDialogViewModelTests
{
    private static ExportNotesDialogViewModel Create() => new(new DateOnly(2025, 8, 1), new DateOnly(2026, 7, 31));

    [Fact]
    public void IsValid_IsTrue_ForDefaultRange()
    {
        var viewModel = Create();

        Assert.True(viewModel.IsValid);
        Assert.Null(viewModel.ValidationMessage);
        Assert.False(viewModel.HasValidationMessage);
    }

    [Fact]
    public void IsValid_IsTrue_WhenFromEqualsTo()
    {
        var viewModel = Create();
        viewModel.To = viewModel.From;

        Assert.True(viewModel.IsValid);
    }

    [Fact]
    public void ValidationMessage_IsSet_WhenFromIsAfterTo()
    {
        var viewModel = Create();
        viewModel.From = new DateTime(2026, 8, 1);

        Assert.False(viewModel.IsValid);
        Assert.True(viewModel.HasValidationMessage);
        Assert.NotNull(viewModel.ValidationMessage);
        Assert.Null(viewModel.TryCreateRequest());
    }

    [Fact]
    public void ValidationMessage_IsSet_WhenFromIsMissing()
    {
        var viewModel = Create();
        viewModel.From = null;

        Assert.False(viewModel.IsValid);
        Assert.NotNull(viewModel.ValidationMessage);
        Assert.Null(viewModel.TryCreateRequest());
    }

    [Fact]
    public void ValidationMessage_IsSet_WhenToIsMissing()
    {
        var viewModel = Create();
        viewModel.To = null;

        Assert.False(viewModel.IsValid);
        Assert.Null(viewModel.TryCreateRequest());
    }

    [Fact]
    public void ValidationMessage_Clears_WhenRangeIsCorrected()
    {
        var viewModel = Create();
        viewModel.From = new DateTime(2026, 8, 1);
        viewModel.From = new DateTime(2025, 9, 1);

        Assert.True(viewModel.IsValid);
        Assert.Null(viewModel.ValidationMessage);
    }

    [Fact]
    public void TryCreateRequest_ReturnsChoice_ForValidInput()
    {
        var viewModel = Create();
        viewModel.IncludeArchived = true;

        var request = viewModel.TryCreateRequest();

        Assert.Equal(new ExportNotesRequest(new DateOnly(2025, 8, 1), new DateOnly(2026, 7, 31), true), request);
    }

    [Fact]
    public void IncludeArchived_IsOff_ByDefault() => Assert.False(Create().IncludeArchived);
}
