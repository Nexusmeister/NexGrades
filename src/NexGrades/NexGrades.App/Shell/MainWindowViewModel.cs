using NexGrades.App.Updates;

namespace NexGrades.App.Shell;

/// <summary>
/// View model of the main window; the window exposes it as <c>MainWindow.ViewModel</c>.
/// </summary>
/// <param name="updateNotice">The notice that a downloaded update will be installed on exit.</param>
public class MainWindowViewModel(UpdateNoticeViewModel updateNotice)
{
    /// <summary>The window's notice that an update is downloaded, with its "Restart now" action.</summary>
    public UpdateNoticeViewModel UpdateNotice { get; } = updateNotice;
}
