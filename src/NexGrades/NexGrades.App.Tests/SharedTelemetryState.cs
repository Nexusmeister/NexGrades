namespace NexGrades.App.Tests;

/// <summary>
/// Tests that touch the process-wide <c>NexGradesTelemetry.ActivitySource</c> listeners run one at a time (xUnit
/// does not parallelize within a collection), so listeners from one test cannot show up in another.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SharedTelemetryState
{
    public const string Name = "Telemetry";
}
