using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexGrades.App.Infrastructure;
using NexGrades.App.Logging;
using NexGrades.Common.Storage;

namespace NexGrades.App.Tests;

public class StartupDiagnosticsTests
{
    private const string Secret = "ZZ-SECRET";

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "NexGrades";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static CapturingLogger<StartupDiagnosticsTests> Run(string connectionString, string environment = "Production")
    {
        var logger = new CapturingLogger<StartupDiagnosticsTests>();
        var paths = new AppDataPaths(Path.Combine(Path.GetTempPath(), "nexgrades-startup-test"));

        StartupDiagnostics.LogStartup(logger, new FakeEnvironment(environment), new DatabaseLocation(connectionString, IsOverride: false), paths);

        return logger;
    }

    [Fact]
    public void LogStartup_LogsVersionOperatingSystemRuntimeAndEnvironment()
    {
        var logger = Run("Data Source=C:\\data\\app.db", "Staging");

        var first = logger.Entries[0];
        Assert.Equal(LogLevel.Information, first.Level);
        Assert.StartsWith("NexGrades ", first.Message, StringComparison.Ordinal);
        Assert.Contains(RuntimeInformation.OSDescription, first.Message, StringComparison.Ordinal);
        Assert.Contains(RuntimeInformation.FrameworkDescription, first.Message, StringComparison.Ordinal);
        Assert.Contains("environment Staging", first.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LogStartup_LogsDatabaseFileAndLogFolder()
    {
        var logger = Run("Data Source=C:\\data\\app.db");

        var second = logger.Entries[1];
        Assert.Contains("C:\\data\\app.db", second.Message, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(Path.GetTempPath(), "nexgrades-startup-test", "logs"), second.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LogStartup_LogsExactlyTwoLinesWithoutException()
    {
        var logger = Run("Data Source=C:\\data\\app.db");

        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, e => Assert.Null(e.Exception));
    }

    [Fact]
    public void LogStartup_NeverLogsConnectionStringPassword_OrOtherKeys()
    {
        var logger = Run($"Data Source=C:\\data\\app.db;Password={Secret};Mode=ReadWrite");

        Assert.Contains("C:\\data\\app.db", logger.Entries[1].Message, StringComparison.Ordinal);
        Assert.All(logger.Entries, e =>
        {
            Assert.DoesNotContain(Secret, e.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("Password", e.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Mode=", e.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void LogStartup_LogsUnknownDatabasePath_WhenConnectionStringIsMalformed()
    {
        var logger = Run($"not a connection string {Secret}");

        Assert.Contains("Database file unknown;", logger.Entries[1].Message, StringComparison.Ordinal);
        Assert.All(logger.Entries, e => Assert.DoesNotContain(Secret, e.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void LogStartup_Throws_ForNullArguments()
    {
        var logger = new CapturingLogger<StartupDiagnosticsTests>();
        var paths = new AppDataPaths(Path.GetTempPath());
        var environment = new FakeEnvironment("x");
        var location = new DatabaseLocation("Data Source=a", IsOverride: false);

        _ = Assert.Throws<ArgumentNullException>(() => StartupDiagnostics.LogStartup(null!, environment, location, paths));
        _ = Assert.Throws<ArgumentNullException>(() => StartupDiagnostics.LogStartup(logger, null!, location, paths));
        _ = Assert.Throws<ArgumentNullException>(() => StartupDiagnostics.LogStartup(logger, environment, null!, paths));
        _ = Assert.Throws<ArgumentNullException>(() => StartupDiagnostics.LogStartup(logger, environment, location, null!));
    }

    [Fact]
    public void LogShuttingDownAndLogShutdown_LogInformationLines()
    {
        var logger = new CapturingLogger<StartupDiagnosticsTests>();

        StartupDiagnostics.LogShuttingDown(logger);
        StartupDiagnostics.LogShutdown(logger);

        Assert.Equal(["NexGrades is shutting down", "NexGrades stopped"], logger.Entries.Select(e => e.Message));
        Assert.All(logger.Entries, e => Assert.Equal(LogLevel.Information, e.Level));
    }
}
