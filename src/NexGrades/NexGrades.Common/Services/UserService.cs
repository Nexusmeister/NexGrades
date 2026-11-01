namespace NexGrades.Common.Services;

/// <summary>
/// Reads the name of the logged-in Windows user from <c>Environment.UserName</c>.
/// </summary>
public class UserService : IUserService
{
    /// <summary>Returns the Windows user name with its first letter upper-cased, or an empty string when there is none.</summary>
    public string GetUserName()
    {
        var user1 = Environment.UserName;

        if (string.IsNullOrWhiteSpace(user1))
        {
            return string.Empty;
        }

        var normalizedUsername = string.Concat(char.ToUpperInvariant(user1[0]).ToString(), user1.AsSpan(1));
        return normalizedUsername;
    }
}
