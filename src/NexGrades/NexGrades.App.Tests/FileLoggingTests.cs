using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using NexGrades.App.Logging;
using NexGrades.Common.Storage;
using NexGrades.Domain.Exceptions;
using Serilog.Events;
using Serilog.Parsing;

namespace NexGrades.App.Tests;

[SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Tests log a few lines to drive the file sink; performance is irrelevant.")]
[SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Tests pass fixed templates.")]
public class FileLoggingTests
{
    private static string[] LogFiles(AppDataPaths paths) =>
        Directory.Exists(paths.LogDirectory) ? Directory.GetFiles(paths.LogDirectory, "nexgrades-*.log") : [];

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value, StringComparer.Ordinal)).Build();

    private static string Format(LogEvent logEvent)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        new LogFileFormatter().Format(logEvent, writer);
        return writer.ToString();
    }

    private static LogEvent Event(LogEventLevel level, string template, Exception? exception = null, string? category = "Some.Category", params (string Name, object Value)[] properties)
    {
        var parsed = new MessageTemplateParser().Parse(template);
        var props = properties.Select(p => new LogEventProperty(p.Name, new ScalarValue(p.Value))).ToList();
        if (category is not null)
        {
            props.Add(new LogEventProperty("SourceContext", new ScalarValue(category)));
        }

        return new LogEvent(new DateTimeOffset(2026, 10, 31, 14, 3, 7, 123, TimeSpan.FromHours(1)), level, exception, parsed, props);
    }

    // Options

    [Fact]
    public void Options_HaveDocumentedDefaults()
    {
        var options = new FileLoggingOptions();

        Assert.Equal(14, options.RetainedFileCount);
        Assert.Equal(3, options.MaxFileSizeMegabytes);
    }

    [Fact]
    public void FromConfiguration_KeepsDefaults_WhenSectionIsMissing() =>
        Assert.Equal(new FileLoggingOptions(), FileLoggingOptions.FromConfiguration(Config()));

    [Fact]
    public void FromConfiguration_UsesConfiguredValues_WhenPresent()
    {
        var options = FileLoggingOptions.FromConfiguration(Config(("Logging:File:RetainedFileCount", "5"), ("Logging:File:MaxFileSizeMegabytes", "10")));

        Assert.Equal(new FileLoggingOptions(5, 10), options);
    }

    [Fact]
    public void FromConfiguration_KeepsOtherDefault_WhenOnlyOneValueIsSet()
    {
        var options = FileLoggingOptions.FromConfiguration(Config(("Logging:File:RetainedFileCount", "5")));

        Assert.Equal(new FileLoggingOptions(5, 3), options);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void Normalize_RaisesValuesToOne_ForZeroOrNegative(int value)
    {
        var options = new FileLoggingOptions(value, value).Normalize();

        Assert.Equal(new FileLoggingOptions(1, 1), options);
    }

    [Fact]
    public void Normalize_KeepsValues_WhenAlreadyPositive() =>
        Assert.Equal(new FileLoggingOptions(2, 7), new FileLoggingOptions(2, 7).Normalize());

    [Fact]
    public void FromConfiguration_ClampsNonPositiveValues()
    {
        var options = FileLoggingOptions.FromConfiguration(Config(("Logging:File:RetainedFileCount", "0"), ("Logging:File:MaxFileSizeMegabytes", "-1")));

        Assert.Equal(new FileLoggingOptions(1, 1), options);
    }

    [Fact]
    public void FromConfiguration_BindsShippedAppSettings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        Assert.True(File.Exists(path), "The App's appsettings.json must be copied to the output folder.");
        var configuration = new ConfigurationBuilder().AddJsonFile(path).Build();

        var options = FileLoggingOptions.FromConfiguration(configuration);

        Assert.Equal(new FileLoggingOptions(14, 3), options);
        Assert.Equal(14, configuration.GetValue<int>("Logging:File:RetainedFileCount"));
    }

    // TryCreate

    [Fact]
    public void TryCreate_WritesDailyFileWithLinePattern()
    {
        using var folder = new TempFolder();
        var paths = new AppDataPaths(folder.Path);

        var logger = FileLogging.TryCreate(paths, new FileLoggingOptions());
        Assert.NotNull(logger);
        logger.ForContext("SourceContext", "Some.Category").Information("Hello {Word}", "world");
        logger.Dispose();

        var file = Assert.Single(LogFiles(paths));
        Assert.Matches(@"nexgrades-\d{8}\.log$", file);
        var line = File.ReadAllLines(file).Single();
        Assert.Matches(new Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2} \[INF\] Some\.Category Hello world$", RegexOptions.None, TimeSpan.FromSeconds(5)), line);
    }

    [Fact]
    public void TryCreate_WritesLine_ForEventWithNumericProperty()
    {
        // The migration service logs "{PendingCount}" as an int; a formatter that cannot render it loses the line.
        using var folder = new TempFolder();
        var paths = new AppDataPaths(folder.Path);

        var logger = FileLogging.TryCreate(paths, new FileLoggingOptions());
        Assert.NotNull(logger);
        logger.ForContext("SourceContext", "Some.Category").Information("Checking; {PendingCount} pending", 3);
        logger.Dispose();

        var text = LogFiles(paths).Select(File.ReadAllText).SingleOrDefault() ?? string.Empty;
        Assert.Contains("Checking; 3 pending", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TryCreate_CreatesMissingLogFolder()
    {
        using var folder = new TempFolder();
        var paths = new AppDataPaths(Path.Combine(folder.Path, "not", "yet"));

        using var logger = FileLogging.TryCreate(paths, new FileLoggingOptions());

        Assert.NotNull(logger);
        Assert.True(Directory.Exists(paths.LogDirectory));
    }

    [Fact]
    public void TryCreate_ReturnsNullAndReportsFailure_WhenLogFolderPathIsAFile()
    {
        using var folder = new TempFolder();
        var paths = new AppDataPaths(folder.Path);
        File.WriteAllText(paths.LogDirectory, "blocking file");
        Exception? reported = null;

        var logger = FileLogging.TryCreate(paths, new FileLoggingOptions(), ex => reported = ex);

        Assert.Null(logger);
        Assert.NotNull(reported);
        Assert.Equal("blocking file", File.ReadAllText(paths.LogDirectory));
    }

    [Fact]
    public void TryCreate_DoesNotThrow_WhenParentOfLogFolderIsAFile()
    {
        using var folder = new TempFolder();
        var blocker = Path.Combine(folder.Path, "blocker");
        File.WriteAllText(blocker, "x");
        var paths = new AppDataPaths(blocker);

        var exception = Record.Exception(() => FileLogging.TryCreate(paths, new FileLoggingOptions())?.Dispose());

        Assert.Null(exception);
    }

    [Fact]
    public void TryCreate_Throws_ForNullArguments()
    {
        using var folder = new TempFolder();

        _ = Assert.Throws<ArgumentNullException>(() => FileLogging.TryCreate(null!, new FileLoggingOptions()));
        _ = Assert.Throws<ArgumentNullException>(() => FileLogging.TryCreate(new AppDataPaths(folder.Path), null!));
    }

    [Fact]
    public void TryCreate_KeepsOnlyRetainedFileCount_AfterAWrite()
    {
        using var folder = new TempFolder();
        var paths = new AppDataPaths(folder.Path);
        Directory.CreateDirectory(paths.LogDirectory);
        foreach (var day in new[] { "20200101", "20200102", "20200103", "20200104", "20200105" })
        {
            File.WriteAllText(Path.Combine(paths.LogDirectory, $"nexgrades-{day}.log"), "old");
        }

        var logger = FileLogging.TryCreate(paths, new FileLoggingOptions(RetainedFileCount: 3));
        Assert.NotNull(logger);
        logger.Information("trigger");
        logger.Dispose();

        var names = LogFiles(paths).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(3, names.Count);
        Assert.DoesNotContain("nexgrades-20200101.log", names);
        Assert.Contains("nexgrades-20200105.log", names);
    }

    [Fact]
    public void TryCreate_NormalizesRetention_WhenConfiguredAsZero()
    {
        using var folder = new TempFolder();
        var paths = new AppDataPaths(folder.Path);
        Directory.CreateDirectory(paths.LogDirectory);
        File.WriteAllText(Path.Combine(paths.LogDirectory, "nexgrades-20200101.log"), "old");

        var logger = FileLogging.TryCreate(paths, new FileLoggingOptions(RetainedFileCount: 0));
        Assert.NotNull(logger);
        logger.Information("trigger");
        logger.Dispose();

        _ = Assert.Single(LogFiles(paths));
        Assert.DoesNotContain(LogFiles(paths), f => f.EndsWith("20200101.log", StringComparison.Ordinal));
    }

    [Fact]
    public void TryCreate_RollsToNumberedFile_WhenSizeLimitIsReached()
    {
        using var folder = new TempFolder();
        var paths = new AppDataPaths(folder.Path);

        var logger = FileLogging.TryCreate(paths, new FileLoggingOptions(MaxFileSizeMegabytes: 1));
        Assert.NotNull(logger);
        var filler = new string('x', 100_000);
        for (var i = 0; i < 15; i++)
        {
            logger.Information(filler);
        }

        logger.Dispose();

        Assert.True(LogFiles(paths).Length > 1);
    }

    [Fact]
    public void TryCreate_WritesRedactedException_ForDomainException()
    {
        using var folder = new TempFolder();
        var paths = new AppDataPaths(folder.Path);

        var logger = FileLogging.TryCreate(paths, new FileLoggingOptions());
        Assert.NotNull(logger);
        logger.Error(new InvariantViolationException("ZZ-SECRET"), "Operation failed");
        logger.Dispose();

        var text = File.ReadAllText(Assert.Single(LogFiles(paths)));
        Assert.Contains("Operation failed", text, StringComparison.Ordinal);
        Assert.Contains(nameof(InvariantViolationException), text, StringComparison.Ordinal);
        Assert.DoesNotContain("ZZ-SECRET", text, StringComparison.Ordinal);
    }

    // Formatter

    [Fact]
    public void Format_WritesTimestampLevelCategoryAndMessage()
    {
        var text = Format(Event(LogEventLevel.Warning, "Value is {Count}", null, "My.Category", ("Count", 1234567)));

        Assert.Equal("2026-10-31 14:03:07.123 +01:00 [WRN] My.Category Value is 1234567" + Environment.NewLine, text);
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose, "VRB")]
    [InlineData(LogEventLevel.Debug, "DBG")]
    [InlineData(LogEventLevel.Information, "INF")]
    [InlineData(LogEventLevel.Warning, "WRN")]
    [InlineData(LogEventLevel.Error, "ERR")]
    [InlineData(LogEventLevel.Fatal, "FTL")]
    public void Format_UsesThreeLetterLevelCode(LogEventLevel level, string code) =>
        Assert.Contains($"[{code}] ", Format(Event(level, "m")), StringComparison.Ordinal);

    [Fact]
    public void Format_OmitsCategory_WhenThereIsNoSourceContext()
    {
        var text = Format(Event(LogEventLevel.Information, "plain", null, null));

        Assert.EndsWith("[INF] plain" + Environment.NewLine, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_UsesInvariantCulture_ForDecimals()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var text = Format(Event(LogEventLevel.Information, "Average {Avg}", null, "C", ("Avg", 2.5m)));

            Assert.Contains("Average 2.5", text, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Format_WritesStringPropertiesWithoutQuotes()
    {
        var text = Format(Event(LogEventLevel.Information, "Version {Version}", null, "C", ("Version", "1.2.3")));

        Assert.Contains("Version 1.2.3", text, StringComparison.Ordinal);
        Assert.DoesNotContain('"', text);
    }

    [Fact]
    public void Format_AppendsRedactedExceptionText_ForDomainException()
    {
        var text = Format(Event(LogEventLevel.Error, "failed", new InvariantViolationException("ZZ-SECRET")));

        Assert.Contains(ExceptionDescriber.RedactedMessage, text, StringComparison.Ordinal);
        Assert.DoesNotContain("ZZ-SECRET", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_AppendsFullMessage_ForFrameworkException()
    {
        var text = Format(Event(LogEventLevel.Error, "failed", new InvalidOperationException("framework says no")));

        Assert.Contains("framework says no", text, StringComparison.Ordinal);
    }
}
