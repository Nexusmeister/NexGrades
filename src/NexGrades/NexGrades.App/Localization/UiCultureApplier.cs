using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace NexGrades.App.Localization;

/// <summary>Makes the resolved cultures the cultures of the whole process.</summary>
public static class UiCultureApplier
{
    private static bool _languageOverridden;

    /// <summary>
    /// Applies the cultures to the process, synchronously, before any window exists. The
    /// <c>CultureInfo.DefaultThreadCurrent*</c> values are essential: <c>App.OnStartup</c> is an <c>async void</c>
    /// method, and the thread culture is an async-local value that is reset to the default after an <c>await</c>, so
    /// setting only <c>CurrentCulture</c> would be lost as soon as startup resumes. They also give every thread pool
    /// and new thread the same cultures. The WPF <see cref="FrameworkElement.LanguageProperty"/> default is replaced
    /// once per process, so XAML bindings format in the format culture instead of en-US; the registration can only
    /// happen once and throws when it is too late, in which case it is left as it is (a static class has no logger,
    /// and the effect is only the en-US formatting of XAML bindings).
    /// </summary>
    /// <param name="cultures">The cultures to apply.</param>
    public static void Apply(UiCultures cultures)
    {
        ArgumentNullException.ThrowIfNull(cultures);

        CultureInfo.DefaultThreadCurrentCulture = cultures.Format;
        CultureInfo.DefaultThreadCurrentUICulture = cultures.Ui;
        CultureInfo.CurrentCulture = cultures.Format;
        CultureInfo.CurrentUICulture = cultures.Ui;

        if (_languageOverridden)
        {
            return;
        }

        _languageOverridden = true;
        try
        {
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(cultures.Format.IetfLanguageTag)));
        }
        catch (ArgumentException)
        {
            // Metadata was already overridden; the default stays.
        }
        catch (InvalidOperationException)
        {
            // The property is already in use, so it is too late to override.
        }
    }
}
