using System.Windows;

namespace NexGrades.App.Infrastructure;

/// <summary>Shows the startup error in a standard Windows message box, which needs no theme or host to work.</summary>
public sealed class MessageBoxStartupErrorDialog : IStartupErrorDialog
{
    /// <inheritdoc />
    public void Show(string message) =>
        _ = MessageBox.Show(message, "NexGrades", MessageBoxButton.OK, MessageBoxImage.Error);
}
