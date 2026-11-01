namespace NexGrades.Common.Services;

/// <summary>
/// Provides the name of the person using the app, for the greeting on the home page. It is an interface so
/// that the name can be replaced in tests.
/// </summary>
public interface IUserService
{
    /// <summary>Returns the user name, or an empty string when none is available.</summary>
    string GetUserName();
}
