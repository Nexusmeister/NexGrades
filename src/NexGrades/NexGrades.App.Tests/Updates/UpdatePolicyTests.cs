using NexGrades.App.Infrastructure;
using NexGrades.App.Tests.TestSupport;
using NexGrades.App.Updates;
using Velopack;

namespace NexGrades.App.Tests.Updates;

public class UpdatePolicyTests
{
    private static UpdateCandidate Stable(string version, bool isDowngrade = false) => FakeUpdateClient.Candidate(version, UpdateChannel.Stable, isDowngrade);

    private static UpdateCandidate Beta(string version, bool isDowngrade = false) => FakeUpdateClient.Candidate(version, UpdateChannel.Beta, isDowngrade);

    [Fact]
    public void ChannelsToCheck_ReturnsStableOnly_ByDefault()
    {
        Assert.Equal([UpdateChannel.Stable], UpdatePolicy.ChannelsToCheck(new UserSettings()));
    }

    [Fact]
    public void ChannelsToCheck_ReturnsStableAndBeta_WhenBetasAreOn()
    {
        Assert.Equal([UpdateChannel.Stable, UpdateChannel.Beta], UpdatePolicy.ChannelsToCheck(new UserSettings { ReceiveBetaUpdates = true }));
    }

    [Fact]
    public void Choose_ReturnsNull_WhenBetaIsInstalledBetasAreOffAndStableIsOlder()
    {
        var current = SemanticVersion.Parse("0.1.2-beta.4");

        var chosen = UpdatePolicy.Choose(current, [Stable("0.1.1")]);

        Assert.Null(chosen);
    }

    [Fact]
    public void Choose_ReturnsStable_WhenItIsNewerThanTheInstalledBeta()
    {
        var current = SemanticVersion.Parse("0.1.2-beta.4");

        var chosen = UpdatePolicy.Choose(current, [Stable("0.1.2")]);

        Assert.NotNull(chosen);
        Assert.Equal("0.1.2", chosen.Version.ToNormalizedString());
    }

    [Fact]
    public void Choose_ReturnsTheHigherVersion_WhenBothChannelsOfferOne()
    {
        var current = SemanticVersion.Parse("0.1.0");

        var chosen = UpdatePolicy.Choose(current, [Stable("0.1.2"), Beta("0.1.3-beta.1")]);

        Assert.NotNull(chosen);
        Assert.Equal(UpdateChannel.Beta, chosen.Channel);
    }

    [Fact]
    public void Choose_ReturnsStable_WhenItIsHigherThanTheBeta()
    {
        var current = SemanticVersion.Parse("0.1.0");

        var chosen = UpdatePolicy.Choose(current, [Beta("0.1.2-beta.9"), Stable("0.1.2")]);

        Assert.NotNull(chosen);
        Assert.Equal(UpdateChannel.Stable, chosen.Channel);
    }

    [Fact]
    public void Choose_ReturnsNull_WhenOfferedVersionEqualsCurrent()
    {
        Assert.Null(UpdatePolicy.Choose(SemanticVersion.Parse("0.1.2"), [Stable("0.1.2")]));
    }

    [Fact]
    public void Choose_ReturnsNull_WhenOfferedVersionIsOlder()
    {
        Assert.Null(UpdatePolicy.Choose(SemanticVersion.Parse("0.2.0"), [Stable("0.1.9")]));
    }

    [Fact]
    public void Choose_RejectsADowngrade_EvenWhenItsVersionLooksNewer()
    {
        Assert.Null(UpdatePolicy.Choose(SemanticVersion.Parse("0.1.0"), [Stable("0.1.5", isDowngrade: true)]));
    }

    [Fact]
    public void Choose_ReturnsNull_ForNoCandidates()
    {
        Assert.Null(UpdatePolicy.Choose(SemanticVersion.Parse("0.1.0"), []));
    }
}
