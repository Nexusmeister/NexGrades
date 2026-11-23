using NexGrades.App.Localization;

namespace NexGrades.App.Features.Settings;

/// <summary>One choice in the language picker.</summary>
/// <param name="Value">The preference the choice stands for.</param>
/// <param name="Label">The text shown to the user.</param>
public sealed record LanguageOption(LanguagePreference Value, string Label);
