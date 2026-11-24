using NexGrades.App.Features.Students;

namespace NexGrades.App.Tests.Features.Students;

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

    [Fact]
    public void DisplayDateEnd_IsOneDayBelowDateOnlyMaxValue() =>
        Assert.Equal(DateOnly.MaxValue.AddDays(-1), DateOnly.FromDateTime(Create().DisplayDateEnd));

    [Fact]
    public void ValidationMessage_IsSet_WhenToIsBeyondDisplayDateEnd()
    {
        var viewModel = Create();
        viewModel.To = DateTime.MaxValue.Date;

        Assert.False(viewModel.IsValid);
        Assert.True(viewModel.HasValidationMessage);
        Assert.Null(viewModel.TryCreateRequest());
    }

    [Fact]
    public void IsValid_IsTrue_WhenToEqualsDisplayDateEnd()
    {
        var viewModel = Create();
        viewModel.To = viewModel.DisplayDateEnd;

        Assert.True(viewModel.IsValid);
        Assert.NotNull(viewModel.TryCreateRequest());
    }

    [Fact]
    public void Revalidate_ReturnsTrue_ForDefaultRange() => Assert.True(Create().Revalidate());

    [Fact]
    public void Revalidate_ReturnsFalse_ForReversedRange()
    {
        var viewModel = Create();
        viewModel.From = new DateTime(2026, 8, 1);

        Assert.False(viewModel.Revalidate());
    }

    [Fact]
    public void Revalidate_ReturnsFalse_WhenDateIsMissing()
    {
        var viewModel = Create();
        viewModel.To = null;

        Assert.False(viewModel.Revalidate());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Revalidate_RaisesValidationPropertyChanged_ForValidAndInvalidInput(bool reversed)
    {
        var viewModel = Create();
        if (reversed)
        {
            viewModel.From = new DateTime(2026, 8, 1);
        }

        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.Revalidate();

        Assert.Contains(nameof(ExportNotesDialogViewModel.ValidationMessage), raised);
        Assert.Contains(nameof(ExportNotesDialogViewModel.HasValidationMessage), raised);
        Assert.Contains(nameof(ExportNotesDialogViewModel.IsValid), raised);
    }
}
