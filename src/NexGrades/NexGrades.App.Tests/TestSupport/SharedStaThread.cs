using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace NexGrades.App.Tests.TestSupport;

/// <summary>
/// One STA thread for the whole test run. A WPF <c>Application</c> and its resources belong to the thread that
/// created them, and only one can exist per process, so every test that loads WPF views must run on this thread.
/// </summary>
internal static class SharedStaThread
{
    private static readonly Lazy<BlockingCollection<Action>> _queue = new(Start);

    public static void Run(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        using var done = new ManualResetEventSlim();
        _queue.Value.Add(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                done.Set();
            }
        });
        done.Wait();
        failure?.Throw();
    }

    private static BlockingCollection<Action> Start()
    {
        var queue = new BlockingCollection<Action>();
        var thread = new Thread(() =>
        {
            foreach (var work in queue.GetConsumingEnumerable())
            {
                work();
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return queue;
    }
}
