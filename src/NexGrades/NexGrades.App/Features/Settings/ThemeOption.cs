using NexGrades.App.Theming;

namespace NexGrades.App.Features.Settings;

/// <summary>One choice in the theme picker.</summary>
/// <param name="Value">The preference the choice stands for.</param>
/// <param name="Label">The text shown to the user.</param>
public sealed record ThemeOption(ThemePreference Value, string Label);
