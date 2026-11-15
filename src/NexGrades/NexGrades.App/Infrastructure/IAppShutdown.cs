namespace NexGrades.App.Infrastructure;

/// <summary>Ends the application the normal way, so the exit handler runs. An abstraction so view models can be tested.</summary>
public interface IAppShutdown
{
    /// <summary>Starts a normal shutdown of the application. Call it on the UI thread.</summary>
    void Shutdown();
}
