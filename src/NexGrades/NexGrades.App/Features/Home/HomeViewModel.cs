using NexGrades.App.Core;
using NexGrades.App.Infrastructure;
using NexGrades.App.Localization;

namespace NexGrades.App.Features.Home;

/// <summary>
/// View model of the home page. The greeting is computed once, when the view model is created, from the local hour.
/// </summary>
public class HomeViewModel : ViewModel
{
    /// <summary>The greeting shown on the home page: "Good Morning" before noon, "Hello" from noon on.</summary>
    public string WelcomeMessage { get; set; } = TextResources.Text("Home_Welcome_Default");

    /// <summary>Creates the view model and sets the greeting for the current user.</summary>
    /// <param name="userService">Provides the user name for the greeting.</param>
    public HomeViewModel(IUserService userService)
    {
        var now = DateTime.Now;
        var username = userService.GetUserName();
        WelcomeMessage = TextResources.Format(now.Hour < 12 ? "Home_Welcome_Morning_Format" : "Home_Welcome_Day_Format", username);
    }
}
