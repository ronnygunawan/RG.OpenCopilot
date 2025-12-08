using System.Threading.Channels;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Channel-based implementation of the job queue with priority support
/// </summary>
internal sealed class ChannelJobQueue : IJobQueue {
    private readonly Channel<BackgroundJob> _channel;
    private readonly BackgroundJobOptions _options;
    private readonly SemaphoreSlim _semaphore;

    public ChannelJobQueue(BackgroundJobOptions options) {
        _options = options;

        var channelOptions = new BoundedChannelOptions(_options.MaxQueueSize > 0 ? _options.MaxQueueSize : int.MaxValue) {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        _channel = Channel.CreateBounded<BackgroundJob>(channelOptions);
        _semaphore = new SemaphoreSlim(1, 1);
    }

    public int Count => _channel.Reader.Count;

    public async Task<bool> EnqueueAsync(BackgroundJob job, CancellationToken cancellationToken = default) {
        try {
            await _channel.Writer.WriteAsync(job, cancellationToken);
            return true;
        }
        catch (ChannelClosedException) {
            return false;
        }
    }

    public async Task<BackgroundJob?> DequeueAsync(CancellationToken cancellationToken = default) {
        try {
            if (_options.EnablePrioritization) {
                return await DequeueWithPriorityAsync(cancellationToken);
            }

            return await _channel.Reader.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException) {
            return null;
        }
        catch (OperationCanceledException) {
            return null;
        }
    }

    private async Task<BackgroundJob?> DequeueWithPriorityAsync(CancellationToken cancellationToken) {
        await _semaphore.WaitAsync(cancellationToken);
        try {
            // Peek at available jobs and select the highest priority one
            var jobs = new List<BackgroundJob>();
            
            // Try to read multiple jobs to find highest priority
            while (_channel.Reader.TryRead(out var job)) {
                jobs.Add(job);
                
                // Limit how many we peek at to avoid blocking too long
                if (jobs.Count >= 10) {
                    break;
                }
            }

            if (jobs.Count == 0) {
                // No jobs available, wait for one
                var nextJob = await _channel.Reader.ReadAsync(cancellationToken);
                return nextJob;
            }

            // Find highest priority job
            var highestPriorityJob = jobs.OrderByDescending(j => j.Priority).First();
            jobs.Remove(highestPriorityJob);

            // Re-enqueue the rest
            foreach (var remainingJob in jobs) {
                await _channel.Writer.WriteAsync(remainingJob, cancellationToken);
            }

            return highestPriorityJob;
        }
        finally {
            _semaphore.Release();
        }
    }

    public void Complete() {
        _channel.Writer.Complete();
    }

    public void Dispose() {
        _semaphore.Dispose();
    }
}
