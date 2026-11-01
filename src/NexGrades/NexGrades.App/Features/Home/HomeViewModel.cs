using NexGrades.App.Core;
using NexGrades.Common.Services;

namespace NexGrades.App.Features.Home;

/// <summary>
/// View model of the home page. The greeting is computed once, when the view model is created, from the local hour.
/// </summary>
public class HomeViewModel : ViewModel
{
    /// <summary>The greeting shown on the home page: "Good Morning" before noon, "Hello" from noon on.</summary>
    public string WelcomeMessage { get; set; } = "Welcome to NexGradeMaker!";

    /// <summary>Creates the view model and sets the greeting for the current user.</summary>
    /// <param name="userService">Provides the user name for the greeting.</param>
    public HomeViewModel(IUserService userService)
    {
        var now = DateTime.Now;
        var username = userService.GetUserName();
        WelcomeMessage = now.Hour < 12 ? $"Good Morning {username}!" : $"Hello {username}!";
    }
}
