using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace NexGrades.App.Tests.TestSupport;

/// <summary>
/// Pins the default cultures of the test process to en-US, so a test sees the same formats and texts on a German
/// machine as on the English CI runner. Tests that need another culture set it themselves and restore it.
/// </summary>
internal static class TestCultureInitializer
{
    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries", Justification = "This is a test assembly; the initializer makes every test run in the same culture before any test code executes.")]
    internal static void PinCultures()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
