using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using NexGrades.App.Infrastructure;

namespace NexGrades.App.Localization;

/// <summary>
/// Loads the saved <see cref="LanguagePreference"/> at startup and saves a change. The language is read once, before
/// any text is shown, and never changes while the app runs: a change is saved and applies after a restart. A
/// settings file that cannot be read or written never stops the app; it starts in the system language.
/// </summary>
/// <param name="store">Reads and saves the preference in <c>settings.json</c>.</param>
/// <param name="logger">Logs failures with a constant message and the exception type only.</param>
public sealed partial class AppLanguageService(IUserSettingsStore store, ILogger<AppLanguageService> logger)
{
    /// <summary>The preference this run uses, as loaded at startup.</summary>
    public LanguagePreference Current { get; private set; } = LanguagePreference.System;

    /// <summary>The preference in the settings file: the loaded one, or the last one saved.</summary>
    public LanguagePreference Saved { get; private set; } = LanguagePreference.System;

    /// <summary>Whether the saved preference differs from the one this run uses, so a restart is needed.</summary>
    public bool IsRestartPending => Saved != Current;

    /// <summary>The cultures this run uses. The current Windows cultures until <see cref="InitializeAsync"/> has run.</summary>
    public UiCultures Cultures { get; private set; } = new(CultureInfo.CurrentUICulture, CultureInfo.CurrentCulture);

    /// <summary>
    /// Reads the saved preference and resolves the cultures; apply them with <see cref="UiCultureApplier.Apply"/>.
    /// It never throws: when the file cannot be read, or holds a value this version does not know, the system
    /// language is used.
    /// </summary>
    /// <param name="windowsUi">The Windows display language.</param>
    /// <param name="windowsFormat">The Windows regional format.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Unreadable settings must not stop the app from starting; it falls back to the system language.")]
    public async Task InitializeAsync(CultureInfo windowsUi, CultureInfo windowsFormat, CancellationToken cancellationToken = default)
    {
        var preference = LanguagePreference.System;
        try
        {
            var settings = await store.LoadAsync(cancellationToken);
            if (Enum.IsDefined(settings.Language))
            {
                preference = settings.Language;
            }
        }
        catch (Exception ex)
        {
            LogLoadFailed(ex.GetType().Name);
        }

        Current = preference;
        Saved = preference;
        Cultures = UiCultureResolver.Resolve(preference, windowsUi, windowsFormat);
        LogLanguage(Cultures.Ui.TwoLetterISOLanguageName);
    }

    /// <summary>Saves <paramref name="preference"/>. It is never applied to the running app; <see cref="IsRestartPending"/> reports the difference.</summary>
    /// <param name="preference">The language to use from the next start.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns><see langword="true"/> when saved; <see langword="false"/> when the save failed and nothing changed.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A preference that cannot be saved, for whatever reason, must not crash the app; the caller reports it.")]
    public async Task<bool> SetAsync(LanguagePreference preference, CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await store.UpdateAsync(settings => settings with { Language = preference }, cancellationToken);
            Saved = preference;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSaveFailed(ex.GetType().Name);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The language setting could not be read, so the system language is used: {ExceptionType}")]
    private partial void LogLoadFailed(string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The language setting could not be saved: {ExceptionType}")]
    private partial void LogSaveFailed(string exceptionType);

    [LoggerMessage(Level = LogLevel.Information, Message = "The app language is {Language}")]
    private partial void LogLanguage(string language);
}
