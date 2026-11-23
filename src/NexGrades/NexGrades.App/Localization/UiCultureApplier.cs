using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace NexGrades.App.Localization;

/// <summary>Makes the resolved cultures the cultures of the whole process.</summary>
public static class UiCultureApplier
{
    private static bool _languageOverridden;
    private static bool _languageOverrideSucceeded;

    /// <summary>
    /// Applies the cultures to the process, synchronously, before any window exists. The
    /// <c>CultureInfo.DefaultThreadCurrent*</c> values are essential: <c>App.OnStartup</c> is an <c>async void</c>
    /// method, and the thread culture is an async-local value that is reset to the default after an <c>await</c>, so
    /// setting only <c>CurrentCulture</c> would be lost as soon as startup resumes. They also give every thread pool
    /// and new thread the same cultures. The WPF <see cref="FrameworkElement.LanguageProperty"/> default is replaced
    /// once per process, so XAML bindings format in the format culture instead of en-US; the registration can only
    /// happen once and throws when it is too late, in which case it is left as it is and <c>false</c> is returned (a
    /// static class has no logger, so the caller logs it; the effect is only the en-US formatting of XAML bindings).
    /// </summary>
    /// <param name="cultures">The cultures to apply.</param>
    /// <returns>
    /// <see langword="false"/> when the WPF language default could not be overridden, so XAML-bound dates stay in
    /// en-US format and the caller should log it; <see langword="true"/> otherwise, including on a repeated call after
    /// a successful override.
    /// </returns>
    public static bool Apply(UiCultures cultures)
    {
        ArgumentNullException.ThrowIfNull(cultures);

        CultureInfo.DefaultThreadCurrentCulture = cultures.Format;
        CultureInfo.DefaultThreadCurrentUICulture = cultures.Ui;
        CultureInfo.CurrentCulture = cultures.Format;
        CultureInfo.CurrentUICulture = cultures.Ui;

        if (_languageOverridden)
        {
            return _languageOverrideSucceeded;
        }

        _languageOverridden = true;
        _languageOverrideSucceeded = TryOverrideLanguage(
            () => FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(cultures.Format.IetfLanguageTag))));
        return _languageOverrideSucceeded;
    }

    /// <summary>
    /// Runs the metadata override and reports whether it took effect. It is a seam so a test can force the failure
    /// without WPF having to refuse a real override.
    /// </summary>
    /// <param name="overrideLanguage">The override; WPF throws when the property is already overridden or in use.</param>
    /// <returns><see langword="false"/> when the override threw one of WPF's two refusals.</returns>
    internal static bool TryOverrideLanguage(Action overrideLanguage)
    {
        ArgumentNullException.ThrowIfNull(overrideLanguage);

        try
        {
            overrideLanguage();
            return true;
        }
        catch (ArgumentException)
        {
            // Metadata was already overridden; the default stays.
            return false;
        }
        catch (InvalidOperationException)
        {
            // The property is already in use, so it is too late to override.
            return false;
        }
    }
}
