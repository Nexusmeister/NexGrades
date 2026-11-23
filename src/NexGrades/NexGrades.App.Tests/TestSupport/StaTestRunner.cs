namespace NexGrades.App.Tests.TestSupport;

/// <summary>
/// Runs an async test body on a dedicated STA thread whose continuations come back to that thread, the way the
/// UI thread behaves. WPF controls such as the snackbar icon can only be created on an STA thread. No WPF
/// application or dispatcher is created, so nothing can hang on one.
/// </summary>
internal static class StaTestRunner
{
    public static Task RunAsync(Func<Task> body)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var context = new PumpContext();
            SynchronizationContext.SetSynchronizationContext(context);
            var task = body();
            _ = task.ContinueWith(
                finished =>
                {
                    if (finished.IsFaulted)
                    {
                        completion.TrySetException(finished.Exception!.InnerExceptions);
                    }
                    else if (finished.IsCanceled)
                    {
                        completion.TrySetCanceled();
                    }
                    else
                    {
                        completion.TrySetResult();
                    }

                    context.Complete();
                },
                TaskScheduler.Default);
            context.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return completion.Task;
    }

    private sealed class PumpContext : SynchronizationContext
    {
        private readonly System.Collections.Concurrent.BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = [];

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException();

        public void Complete() => _queue.CompleteAdding();

        public void Run()
        {
            foreach (var (callback, state) in _queue.GetConsumingEnumerable())
            {
                callback(state);
            }
        }
    }
}
