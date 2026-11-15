namespace NexGrades.App.Updates;

/// <summary>Where <see cref="UpdateCoordinator"/> is in finding and preparing an update.</summary>
public enum UpdateState
{
    /// <summary>The app is not an installation that can update itself (development, Aspire, tests); nothing happens.</summary>
    Inactive,

    /// <summary>No check or download is running and no update is waiting.</summary>
    Idle,

    /// <summary>The release feeds are being checked.</summary>
    Checking,

    /// <summary>An update was found and is being downloaded in the background.</summary>
    Downloading,

    /// <summary>An update is downloaded and will be installed when the app closes.</summary>
    ReadyToInstall,
}
