using MealPlaner.Services;
using System.Collections.Concurrent;

namespace MealPlaner.Services
{
    /// <summary>
    /// Provides an interface for managing background tasks in a queue.
    /// Enables enqueuing and dequeuing of asynchronous tasks that can be processed in the background.
    /// </summary>
    public interface IBackgroundTaskQueue
    {
        void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem);
        Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
    }
}
/// <summary>
/// Queues a new background work item for processing.
/// </summary>
/// <param name="workItem">A function representing the work item to be executed, which accepts a <see cref="CancellationToken"/>.</param>

public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly ConcurrentQueue<Func<CancellationToken, Task>> _workItems = new();
    private readonly SemaphoreSlim _signal = new(0);

    public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
    {
        _workItems.Enqueue(workItem);
        _signal.Release();
    }
    /// <summary>
    /// Dequeues a background work item for execution, awaiting its availability.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the dequeue operation if necessary.</param>
    /// <returns>Returns a function representing the work item to be executed, or null if the operation is canceled.</returns>

    public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken);
        _workItems.TryDequeue(out var workItem);
        return workItem;
    }
}