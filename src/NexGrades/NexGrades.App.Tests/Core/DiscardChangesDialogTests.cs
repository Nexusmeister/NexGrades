using NexGrades.App.Core;
using NexGrades.App.Tests.TestSupport;
using Wpf.Ui.Controls;

namespace NexGrades.App.Tests.Core;

/// <summary>Tests <see cref="DiscardChangesDialog.ConfirmAsync"/>: only the primary button discards.</summary>
public class DiscardChangesDialogTests
{
    [Fact]
    public Task ConfirmAsync_ReturnsTrue_WhenUserChoosesDiscardAsync() => StaTestRunner.RunAsync(async () =>
    {
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.Primary);

        Assert.True(await DiscardChangesDialog.ConfirmAsync(dialogs, DiscardChangesDialog.UnsavedChangesMessage));
        Assert.Equal(1, fake.Shown);
    });

    [Theory]
    [InlineData(ContentDialogResult.None)]
    [InlineData(ContentDialogResult.Secondary)]
    public Task ConfirmAsync_ReturnsFalse_WhenUserKeepsEditingAsync(ContentDialogResult answer) => StaTestRunner.RunAsync(async () =>
    {
        var (dialogs, fake) = FakeContentDialogService.Create(answer);

        Assert.False(await DiscardChangesDialog.ConfirmAsync(dialogs, DiscardChangesDialog.UnsavedChangesMessage));
        Assert.Equal(1, fake.Shown);
    });

    [Fact]
    public async Task ConfirmAsync_Throws_WhenDialogServiceIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => DiscardChangesDialog.ConfirmAsync(null!, "text"));
    }
}
