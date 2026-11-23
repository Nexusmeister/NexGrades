using System.Globalization;

namespace NexGrades.App.Localization;

/// <summary>The two cultures the app runs with: one for text, one for numbers and dates.</summary>
/// <param name="Ui">The culture of the UI text. It decides which resource satellite is read.</param>
/// <param name="Format">The culture that formats numbers, averages and dates.</param>
public sealed record UiCultures(CultureInfo Ui, CultureInfo Format);
