using NexGrades.App.Services;

namespace NexGrades.App.ViewModels;

public class HomeViewModel : ViewModel
{
    public string WelcomeMessage { get; set; } = "Welcome to NexGradeMaker!";

    public HomeViewModel(IUserService userService)
    {
        var now = DateTime.Now;
        var username = userService.GetUserName();
        WelcomeMessage = now.Hour < 12 ? $"Good Morning {username}!" : $"Hello {username}!";
    }
}