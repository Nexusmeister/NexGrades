namespace NexGrades.App.Features.Grades;

/// <summary>
/// What the grid behavior and the save serialization need from any editable cell of the grade sheet: an assessment
/// grade cell or the final grade cell. It keeps both on one code path for editing, error marks and the leave guard.
/// </summary>
internal interface IGradeSheetEditableCell
{
    /// <summary>The text the user sees and types.</summary>
    string Text { get; }

    /// <summary>True when the user may type into the cell now.</summary>
    bool IsEditable { get; }

    /// <summary>True when the text is not legal or could not be saved.</summary>
    bool HasError { get; set; }

    /// <summary>What the cell looks like, for styling.</summary>
    GradeCellVisualState VisualState { get; }

    /// <summary>A whole-sentence explanation of the cell's state. Empty for a normal assessment grade cell; always set for the final grade cell.</summary>
    string Description { get; }

    /// <summary>True while a save of this cell runs.</summary>
    bool IsSaving { get; set; }

    /// <summary>True when the text was committed again while a save ran.</summary>
    bool SaveQueued { get; set; }

    /// <summary>True when the cell holds input that is not saved yet.</summary>
    bool HasUnsavedText { get; }

    /// <summary>Saves the cell again, but only if its last save failed or its text was not legal.</summary>
    void RetrySaveIfFailed();
}
