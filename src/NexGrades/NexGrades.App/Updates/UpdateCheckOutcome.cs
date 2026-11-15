namespace NexGrades.App.Updates;

/// <summary>The result of <see cref="UpdateCoordinator.CheckNowAsync"/>, for the settings page to word.</summary>
public enum UpdateCheckOutcome
{
    /// <summary>The app is not an installation that can update itself, so nothing was checked.</summary>
    NotInstalled,

    /// <summary>Every checked channel answered and none offered a newer version.</summary>
    UpToDate,

    /// <summary>A newer version was found; it is being downloaded or is ready to install.</summary>
    UpdateFound,

    /// <summary>No channel could be checked, typically because there is no internet connection.</summary>
    Offline,
}
