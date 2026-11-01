using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NexGrades.Data.Diagnostics;
using NexGrades.ServiceDefaults;

namespace NexGrades.App.Tests;

/// <summary>
/// Guards the telemetry switches: nothing is exported unless an OTLP endpoint is configured, and the
/// listeners exist as soon as <c>InitializeTelemetry</c> has run.
/// </summary>
[Collection(SharedTelemetryState.Name)]
public class TelemetryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsOtlpExportEnabled_ReturnsFalse_ForMissingOrBlankEndpoint(string? endpoint)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ServiceDefaultsExtensions.OtlpEndpointKey] = endpoint })
            .Build();

        Assert.False(ServiceDefaultsExtensions.IsOtlpExportEnabled(configuration));
    }

    [Fact]
    public void IsOtlpExportEnabled_ReturnsFalse_WhenTheKeyIsAbsent()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.False(ServiceDefaultsExtensions.IsOtlpExportEnabled(configuration));
    }

    [Fact]
    public void IsOtlpExportEnabled_ReturnsTrue_WhenAnEndpointIsSet()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ServiceDefaultsExtensions.OtlpEndpointKey] = "http://localhost:4317" })
            .Build();

        Assert.True(ServiceDefaultsExtensions.IsOtlpExportEnabled(configuration));
    }

    [Fact]
    public void InitializeTelemetry_CreatesListeners_ForTheAppActivitySource()
    {
        // Precondition: no other test left a listener behind, otherwise the assertions below prove nothing.
        Assert.False(NexGradesTelemetry.ActivitySource.HasListeners());

        var builder = Host.CreateApplicationBuilder();
        // Overrides an OTEL_EXPORTER_OTLP_ENDPOINT from the developer's environment so the test never exports.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [ServiceDefaultsExtensions.OtlpEndpointKey] = string.Empty });
        builder.AddServiceDefaults(NexGradesTelemetry.Name);

        using (var host = builder.Build())
        {
            // Providers are created lazily; before InitializeTelemetry (i.e. before IHost.StartAsync) nothing listens.
            Assert.False(NexGradesTelemetry.ActivitySource.HasListeners());

            _ = host.Services.InitializeTelemetry();

            Assert.True(NexGradesTelemetry.ActivitySource.HasListeners());
        }

        // Disposing the host disposes the tracer provider and removes its listener again.
        Assert.False(NexGradesTelemetry.ActivitySource.HasListeners());
    }
}
