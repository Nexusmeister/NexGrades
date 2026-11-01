namespace NexGrades.App.Features.Students;

/// <summary>
/// One note as shown in the list. Dates are pre-formatted with the current UI culture so the view does not
/// depend on XAML string formats.
/// </summary>
public sealed record NoteRow(
    int Id,
    string Text,
    string? Category,
    string CreatedOnDisplay,
    string? EditedOnDisplay,
    bool IsArchived,
    string? ArchivedOnDisplay);
