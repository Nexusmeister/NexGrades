using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using NexGrades.App.Infrastructure;

namespace NexGrades.App.Theming;

/// <summary>
/// Loads the saved <see cref="ThemePreference"/>, applies it, and saves a change. A settings file that cannot be read
/// or written never stops the app: it starts in the system theme, and a change that cannot be saved still applies
/// until the app closes.
/// </summary>
/// <param name="store">Reads and saves the preference in <c>settings.json</c>.</param>
/// <param name="applier">Shows the theme.</param>
/// <param name="logger">Logs failures with a constant message and the exception type only.</param>
public sealed partial class AppThemeService(IUserSettingsStore store, IThemeApplier applier, ILogger<AppThemeService> logger)
{
    /// <summary>The preference in effect: the loaded one, or the last one set.</summary>
    public ThemePreference Current { get; private set; } = ThemePreference.System;

    /// <summary>
    /// Reads the saved preference and applies it. Call it before the main window is shown. It never throws: when
    /// the file cannot be read, or holds a value this version does not know, the system theme is used.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Unreadable settings must not stop the app from starting; it falls back to the system theme.")]
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var preference = ThemePreference.System;
        try
        {
            var settings = await store.LoadAsync(cancellationToken);
            if (Enum.IsDefined(settings.Theme))
            {
                preference = settings.Theme;
            }
        }
        catch (Exception ex)
        {
            LogLoadFailed(ex.GetType().Name);
        }

        Current = preference;
        applier.Apply(preference);
    }

    /// <summary>
    /// Applies <paramref name="preference"/> at once, then saves it.
    /// </summary>
    /// <param name="preference">The theme to use.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns><see langword="true"/> when the preference was saved; <see langword="false"/> when it applies only until the app closes.</returns>
    public async Task<bool> SetAsync(ThemePreference preference, CancellationToken cancellationToken = default)
    {
        applier.Apply(preference);
        Current = preference;

        try
        {
            _ = await store.UpdateAsync(settings => settings with { Theme = preference }, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(ex.GetType().Name);
            return false;
        }
    }

    /// <summary>Hands the main window to the applier, so the system theme is followed live.</summary>
    /// <param name="window">The main window.</param>
    public void AttachWindow(Window window) => applier.AttachWindow(window);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The theme setting could not be read, so the system theme is used: {ExceptionType}")]
    private partial void LogLoadFailed(string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The theme setting could not be saved: {ExceptionType}")]
    private partial void LogSaveFailed(string exceptionType);
}
