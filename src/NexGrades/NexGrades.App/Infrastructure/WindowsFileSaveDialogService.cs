using Microsoft.Win32;

namespace NexGrades.App.Infrastructure;

/// <summary>Windows implementation of <see cref="IFileSaveDialogService"/>, built on the WPF <c>SaveFileDialog</c>.</summary>
public sealed class WindowsFileSaveDialogService : IFileSaveDialogService
{
    /// <inheritdoc />
    public string? SelectSaveFile(string suggestedFileName)
    {
        // Created per call, not in the constructor: the DI graph is also resolved headless in tests.
        SaveFileDialog dialog = new()
        {
            FileName = suggestedFileName,
            DefaultExt = ".xlsx",
            AddExtension = true,
            OverwritePrompt = true,
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
