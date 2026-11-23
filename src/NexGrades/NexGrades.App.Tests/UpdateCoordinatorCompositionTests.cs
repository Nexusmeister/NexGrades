using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NexGrades.App.Extensions;
using NexGrades.App.Updates;

namespace NexGrades.App.Tests;

/// <summary>
/// The updater must be inert outside an installed app: the real composition hands out a coordinator that never
/// reaches Velopack, the network or the disk, so development, Aspire and every test run stay unaffected.
/// </summary>
public class UpdateCoordinatorCompositionTests
{
    [Fact]
    public async Task CheckNowAsync_ReturnsNotInstalledWithoutNetwork_ForTheCoordinatorFromTheRealComposition()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nexgrades-update-di-{Guid.NewGuid():N}.db");
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:sqlite"] = $"Data Source={databasePath}",

            // A feed that would fail loudly if anything contacted it.
            ["Updates:Repository"] = "https://invalid.invalid/nobody/nothing",
        });
        builder.Services.AddShell().AddDomain().AddFeatures();
        builder.AddDatabase();
        using var host = builder.Build();

        var coordinator = host.Services.GetRequiredService<UpdateCoordinator>();
        var outcome = await coordinator.CheckNowAsync();
        await coordinator.CheckOnStartupAsync();

        Assert.Equal(UpdateCheckOutcome.NotInstalled, outcome);
        Assert.Equal(UpdateState.Inactive, coordinator.State);
        Assert.False(coordinator.IsAvailable);
        Assert.False(coordinator.ApplyPendingOnExit());
        Assert.Same(coordinator, host.Services.GetRequiredService<UpdateCoordinator>());
        Assert.False(File.Exists(databasePath));
    }

    [Fact]
    public void VelopackUpdateClient_IsNotInstalled_WhenRunUnderTests()
    {
        var client = new VelopackUpdateClient(Microsoft.Extensions.Options.Options.Create(new UpdateFeedOptions()));

        Assert.False(client.IsInstalled);
        Assert.Null(client.CurrentVersion);
        Assert.Null(client.PendingRestart);
    }
}
