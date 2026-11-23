using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using NexGrades.App.Updates;
using Velopack;

namespace NexGrades.App.Tests.TestSupport;

/// <summary>
/// A scriptable <see cref="IUpdateClient"/>: per-channel check answers (or failures), a download that can be held open,
/// and a record of every call so tests can assert that nothing was touched.
/// </summary>
[SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "Tests await gates they own and release themselves.")]
internal sealed class FakeUpdateClient : IUpdateClient
{
    private readonly Lock _gate = new();
    private readonly List<UpdateChannel> _checks = [];
    private readonly List<(UpdateCandidate Candidate, bool Restart)> _applied = [];
    private int _downloads;

    public bool Installed { get; set; } = true;

    public Exception? IsInstalledFailure { get; set; }

    public SemanticVersion? Current { get; set; } = SemanticVersion.Parse("0.1.0");

    public UpdateCandidate? Pending { get; set; }

    /// <summary>Answers a check per channel; a channel without an entry has no update.</summary>
    public Dictionary<UpdateChannel, Func<CancellationToken, Task<UpdateCandidate?>>> Checkers { get; } = [];

    /// <summary>When set, the download waits for it; otherwise it completes at once.</summary>
    public TaskCompletionSource? DownloadGate { get; set; }

    public Exception? DownloadFailure { get; set; }

    public int PropertyReads { get; private set; }

    public IReadOnlyList<UpdateChannel> Checks
    {
        get
        {
            lock (_gate)
            {
                return [.. _checks];
            }
        }
    }

    public int Downloads => Volatile.Read(ref _downloads);

    public IReadOnlyList<(UpdateCandidate Candidate, bool Restart)> Applied
    {
        get
        {
            lock (_gate)
            {
                return [.. _applied];
            }
        }
    }

    public bool IsInstalled
    {
        get
        {
            PropertyReads++;
            return IsInstalledFailure is null ? Installed : throw IsInstalledFailure;
        }
    }

    public SemanticVersion? CurrentVersion
    {
        get
        {
            PropertyReads++;
            return Installed ? Current : null;
        }
    }

    public UpdateCandidate? PendingRestart
    {
        get
        {
            PropertyReads++;
            return Pending;
        }
    }

    public static UpdateCandidate Candidate(string version, UpdateChannel channel = UpdateChannel.Stable, bool isDowngrade = false) =>
        new(SemanticVersion.Parse(version), channel, isDowngrade);

    public FakeUpdateClient Returns(UpdateChannel channel, UpdateCandidate? candidate)
    {
        Checkers[channel] = _ => Task.FromResult(candidate);
        return this;
    }

    public FakeUpdateClient Fails(UpdateChannel channel, Exception? failure = null)
    {
        Checkers[channel] = _ => Task.FromException<UpdateCandidate?>(failure ?? new HttpRequestException("offline"));
        return this;
    }

    public Task<UpdateCandidate?> CheckAsync(UpdateChannel channel, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _checks.Add(channel);
        }

        return Checkers.TryGetValue(channel, out var checker) ? checker(cancellationToken) : Task.FromResult<UpdateCandidate?>(null);
    }

    public async Task DownloadAsync(UpdateCandidate candidate, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _downloads);
        if (DownloadGate is not null)
        {
            await DownloadGate.Task;
        }

        if (DownloadFailure is not null)
        {
            throw DownloadFailure;
        }
    }

    public void ApplyOnExit(UpdateCandidate candidate, bool restart)
    {
        lock (_gate)
        {
            _applied.Add((candidate, restart));
        }
    }
}
