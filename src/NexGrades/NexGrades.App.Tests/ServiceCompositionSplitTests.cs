using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NexGrades.App.Extensions;
using NexGrades.App.Shell;
using NexGrades.App.Updates;

namespace NexGrades.App.Tests;

/// <summary>
/// Guards the split of the service registration into <c>AddShell</c>, <c>AddDomain</c> and <c>AddFeatures</c>, so a
/// registration cannot drift into the wrong group.
/// </summary>
public sealed class ServiceCompositionSplitTests
{
    [Fact]
    public void AddDomain_RegistersOnlyDomainTypes_WhenCalled()
    {
        var services = new ServiceCollection();

        services.AddDomain();

        Assert.NotEmpty(services);
        Assert.All(services, d => Assert.StartsWith("NexGrades.Domain", d.ServiceType.Namespace, StringComparison.Ordinal));
    }

    [Fact]
    public void AddFeatures_RegistersOnlyFeatureTypes_WhenCalled()
    {
        var services = new ServiceCollection();

        services.AddFeatures();

        Assert.NotEmpty(services);
        Assert.All(services, d => Assert.StartsWith("NexGrades.App.Features", d.ServiceType.Namespace, StringComparison.Ordinal));
    }

    [Fact]
    public void AddShell_RegistersApplicationHostServiceBeforeUpdateStartupService_WhenCalled()
    {
        var services = new ServiceCollection();

        services.AddShell();

        var hosted = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .Select(d => d.ImplementationType)
            .ToList();
        var hostIndex = hosted.IndexOf(typeof(ApplicationHostService));
        var updateIndex = hosted.IndexOf(typeof(UpdateStartupService));
        Assert.True(hostIndex >= 0);
        Assert.True(updateIndex >= 0);
        Assert.True(hostIndex < updateIndex);
    }
}
