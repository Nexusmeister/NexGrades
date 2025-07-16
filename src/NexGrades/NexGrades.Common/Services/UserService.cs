namespace NexGrades.App.Services;

public class UserService : IUserService
{
    public string GetUserName()
    {
        var user1 = Environment.UserName;

        if (string.IsNullOrWhiteSpace(user1))
        {
            return string.Empty;
        }

        var normalizedUsername = string.Concat(user1[0].ToString().ToUpper(), user1.AsSpan(1));
        return normalizedUsername;
    }
}