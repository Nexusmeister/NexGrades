namespace NexGrades.App.Infrastructure;

/// <summary>
/// Tells the user that the application cannot start. An abstraction so <see cref="StartupFailureHandler"/> can be
/// tested without a window.
/// </summary>
public interface IStartupErrorDialog
{
    /// <summary>Shows <paramref name="message"/> as a modal error and returns once the user has dismissed it.</summary>
    /// <param name="message">The text to show; it never contains exception text.</param>
    void Show(string message);
}
