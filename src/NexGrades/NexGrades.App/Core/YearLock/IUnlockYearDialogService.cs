namespace NexGrades.App.Core.YearLock;

/// <summary>Asks the teacher why and for how long a closed school year should be unlocked.</summary>
public interface IUnlockYearDialogService
{
    /// <summary>Shows the unlock dialog.</summary>
    /// <param name="schoolYearLabel">The label of the year, e.g. "2025/26", shown in the dialog title.</param>
    /// <param name="cancellationToken">Closes the dialog.</param>
    /// <returns>The entered values, or <see langword="null"/> when the teacher cancelled.</returns>
    Task<UnlockRequest?> AskAsync(string schoolYearLabel, CancellationToken cancellationToken = default);
}
