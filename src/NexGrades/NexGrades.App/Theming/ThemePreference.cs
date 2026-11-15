using System.Text.Json.Serialization;

namespace NexGrades.App.Theming;

/// <summary>
/// Which colour theme the app uses. It is stored in <c>settings.json</c> by name, so reordering the members never
/// changes what an existing file means.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ThemePreference
{
    /// <summary>Follow the Windows light or dark setting, including changes while the app runs.</summary>
    System = 0,

    /// <summary>Always use the light theme.</summary>
    Light = 1,

    /// <summary>Always use the dark theme.</summary>
    Dark = 2,
}
