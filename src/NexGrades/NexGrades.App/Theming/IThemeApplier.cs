using System.Windows;

namespace NexGrades.App.Theming;

/// <summary>
/// Puts a <see cref="ThemePreference"/> on screen. It is a seam so <see cref="AppThemeService"/> can be tested
/// without WPF.
/// </summary>
public interface IThemeApplier
{
    /// <summary>Switches the running app to <paramref name="preference"/> at once.</summary>
    /// <param name="preference">The theme to show.</param>
    void Apply(ThemePreference preference);

    /// <summary>
    /// Tells the applier which window to keep in step with Windows while the preference is
    /// <see cref="ThemePreference.System"/>. The current preference is applied again to it.
    /// </summary>
    /// <param name="window">The main window.</param>
    void AttachWindow(Window window);
}
