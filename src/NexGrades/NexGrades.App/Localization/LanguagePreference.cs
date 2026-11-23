using System.Text.Json.Serialization;

namespace NexGrades.App.Localization;

/// <summary>
/// Which language the app shows. It is stored in <c>settings.json</c> by name, so reordering the members never
/// changes what an existing file means.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LanguagePreference
{
    /// <summary>Follow the Windows display language: German when it is German, English otherwise.</summary>
    System = 0,

    /// <summary>Always show English.</summary>
    English = 1,

    /// <summary>Always show German.</summary>
    German = 2,
}
