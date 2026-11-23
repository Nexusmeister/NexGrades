using NexGrades.App.Localization;

namespace NexGrades.App.Tests.Localization;

/// <summary>Tests the language-override seam of <see cref="UiCultureApplier"/>.</summary>
/// <remarks>
/// Never calls <see cref="UiCultureApplier.Apply"/>: it overrides WPF metadata once per process.
/// </remarks>
public sealed class UiCultureApplierTests
{
    [Theory]
    [MemberData(nameof(Refusals))]
    public void TryOverrideLanguage_ReturnsFalse_WhenActionThrowsWpfRefusal(Exception refusal)
    {
        var result = UiCultureApplier.TryOverrideLanguage(() => throw refusal);

        Assert.False(result);
    }

    public static TheoryData<Exception> Refusals =>
    [
        new InvalidOperationException("in use"),
        NewAlreadyOverridden("overrideLanguage"),
    ];

    private static ArgumentException NewAlreadyOverridden(string overrideLanguage) =>
        new("already overridden", nameof(overrideLanguage));

    [Fact]
    public void TryOverrideLanguage_ReturnsTrue_WhenActionSucceeds()
    {
        var ran = false;

        var result = UiCultureApplier.TryOverrideLanguage(() => ran = true);

        Assert.True(result);
        Assert.True(ran);
    }

    [Fact]
    public void TryOverrideLanguage_Throws_WhenActionIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => UiCultureApplier.TryOverrideLanguage(null!));
    }

    [Fact]
    public void TryOverrideLanguage_Propagates_WhenActionThrowsOtherException()
    {
        Assert.Throws<NotSupportedException>(() => UiCultureApplier.TryOverrideLanguage(() => throw new NotSupportedException()));
    }
}
