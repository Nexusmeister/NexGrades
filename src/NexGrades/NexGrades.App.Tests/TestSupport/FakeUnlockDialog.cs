using NexGrades.App.Core.YearLock;

namespace NexGrades.App.Tests.TestSupport;

/// <summary>An <see cref="IUnlockYearDialogService"/> that shows nothing and answers with a fixed result.</summary>
internal sealed class FakeUnlockDialog(UnlockRequest? answer = null) : IUnlockYearDialogService
{
    public Task<UnlockRequest?> AskAsync(string schoolYearLabel, CancellationToken cancellationToken = default) =>
        Task.FromResult(answer);
}
