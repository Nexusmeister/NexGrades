namespace NexGrades.App.Features.Students;

/// <summary>
/// One note as shown in the list. Dates are pre-formatted with the current UI culture so the view does not
/// depend on XAML string formats.
/// </summary>
/// <param name="Id">The note id.</param>
/// <param name="Text">The note text.</param>
/// <param name="Category">The category, or <see langword="null"/> when none is set.</param>
/// <param name="CreatedOnDisplay">The creation time, formatted for the current UI culture.</param>
/// <param name="EditedOnDisplay">Localized "Edited ..." text, or <see langword="null"/> when the note was never edited.</param>
/// <param name="IsArchived">Whether the note is archived.</param>
/// <param name="ArchivedOnDisplay">Localized "Archived ..." text, or <see langword="null"/> when the note is active.</param>
public sealed record NoteRow(
    int Id,
    string Text,
    string? Category,
    string CreatedOnDisplay,
    string? EditedOnDisplay,
    bool IsArchived,
    string? ArchivedOnDisplay);
