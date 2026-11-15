using System.Windows;

namespace NexGrades.App.Infrastructure;

/// <summary><see cref="IAppShutdown"/> through <see cref="Application.Shutdown()"/>, which raises the app's exit handler.</summary>
public sealed class WpfAppShutdown : IAppShutdown
{
    /// <inheritdoc />
    public void Shutdown() => Application.Current?.Shutdown();
}
