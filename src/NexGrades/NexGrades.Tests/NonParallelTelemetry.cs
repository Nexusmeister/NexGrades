namespace NexGrades.Tests;

// ActivityListener and MeterListener see the whole process, so these tests run in a non-parallel collection
// (xUnit runs it alone) and additionally filter every capture by operation name.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NonParallelTelemetry
{
    public const string Name = "Telemetry";
}
