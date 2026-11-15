using System.Windows;
using Wpf.Ui.Appearance;

namespace NexGrades.App.Theming;

/// <summary>
/// <see cref="IThemeApplier"/> on top of WPF-UI's <see cref="ApplicationThemeManager"/>. For
/// <see cref="ThemePreference.System"/> it also watches the window so a Windows theme change is followed live; an
/// explicit Light or Dark stops that. The constructor touches no WPF objects, so the instance can be created before
/// the application's resources exist.
/// </summary>
public sealed class WpfThemeApplier : IThemeApplier
{
    private Window? _window;
    private bool _watching;
    private ThemePreference _preference = ThemePreference.System;

    /// <inheritdoc/>
    public void Apply(ThemePreference preference)
    {
        _preference = preference;

        if (preference == ThemePreference.System)
        {
            ApplicationThemeManager.ApplySystemTheme();
            if (_window is not null && !_watching)
            {
                SystemThemeWatcher.Watch(_window);
                _watching = true;
            }

            return;
        }

        if (_window is not null && _watching)
        {
            SystemThemeWatcher.UnWatch(_window);
            _watching = false;
        }

        ApplicationThemeManager.Apply(preference == ThemePreference.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light);
    }

    /// <inheritdoc/>
    public void AttachWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;
        _watching = false;
        Apply(_preference);
    }
}
