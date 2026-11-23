using NexGrades.App.Infrastructure;

namespace NexGrades.App.Tests.TestSupport;

/// <summary>Counts shutdown requests instead of ending the process.</summary>
internal sealed class FakeAppShutdown : IAppShutdown
{
    public int Calls { get; private set; }

    public void Shutdown() => Calls++;
}
