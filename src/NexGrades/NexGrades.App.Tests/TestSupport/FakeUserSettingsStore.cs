using NexGrades.App.Infrastructure;

namespace NexGrades.App.Tests.TestSupport;

/// <summary>An in-memory <see cref="IUserSettingsStore"/> that counts calls and can be made to fail.</summary>
internal sealed class FakeUserSettingsStore : IUserSettingsStore
{
    public UserSettings Current { get; set; } = new();

    public Exception? LoadFailure { get; set; }

    public Exception? UpdateFailure { get; set; }

    public int LoadCalls { get; private set; }

    public int UpdateCalls { get; private set; }

    public Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        LoadCalls++;
        return LoadFailure is null ? Task.FromResult(Current) : Task.FromException<UserSettings>(LoadFailure);
    }

    public Task<UserSettings> UpdateAsync(Func<UserSettings, UserSettings> update, CancellationToken cancellationToken = default)
    {
        UpdateCalls++;
        if (UpdateFailure is not null)
        {
            return Task.FromException<UserSettings>(UpdateFailure);
        }

        Current = update(Current);
        return Task.FromResult(Current);
    }
}
