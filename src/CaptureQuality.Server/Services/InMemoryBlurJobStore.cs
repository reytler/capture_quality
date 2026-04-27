using System.Collections.Concurrent;
using System.Threading.Channels;
using CaptureQuality.Models;

namespace CaptureQuality.Server.Services;

public sealed class InMemoryBlurJobStore : IBlurJobStore
{
    private static readonly TimeSpan JobRetention = TimeSpan.FromMinutes(20);
    private readonly ConcurrentDictionary<string, BlurJob> _jobs = new();
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>();

    public ChannelReader<string> QueueReader => _queue.Reader;

    public async Task<CreateBlurJobResponse> EnqueueAsync(
        string fileName,
        string contentType,
        byte[] imageBytes,
        CancellationToken cancellationToken)
    {
        CleanupExpiredJobs();

        var job = new BlurJob
        {
            FileName = fileName,
            ContentType = contentType,
            ImageBytes = imageBytes,
            Progress = 5,
            Stage = "queued",
            Message = "Upload received"
        };

        _jobs[job.Id] = job;
        await _queue.Writer.WriteAsync(job.Id, cancellationToken);

        return new CreateBlurJobResponse
        {
            JobId = job.Id,
            Status = job.State.ToString(),
            Progress = job.Progress,
            CreatedAt = job.CreatedAt
        };
    }

    public BlurJobUpdateDto? Get(string jobId)
    {
        CleanupExpiredJobs();
        return _jobs.TryGetValue(jobId, out var job) ? ToDto(job) : null;
    }

    public BlurJob? GetJob(string jobId)
    {
        CleanupExpiredJobs();
        return _jobs.TryGetValue(jobId, out var job) ? job : null;
    }

    public bool TrySubscribe(string jobId)
    {
        CleanupExpiredJobs();
        return _jobs.ContainsKey(jobId);
    }

    public bool TryCancel(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return false;
        }

        job.Cancellation.Cancel();
        if (job.State == BlurJobState.Queued)
        {
            job.State = BlurJobState.Canceled;
            job.Progress = 100;
            job.Stage = "canceled";
            job.Message = "Job canceled before processing";
            job.CompletedAt = DateTime.UtcNow;
        }

        return true;
    }

    public bool TryMarkRunning(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job) || job.State == BlurJobState.Canceled)
        {
            return false;
        }

        job.State = BlurJobState.Running;
        job.Progress = Math.Max(job.Progress, 10);
        job.Stage = "running";
        job.Message = "Processing started";
        job.StartedAt = DateTime.UtcNow;
        return true;
    }

    public bool TryMarkProgress(string jobId, int progress, string stage, string message)
    {
        if (!_jobs.TryGetValue(jobId, out var job) || job.State is BlurJobState.Canceled or BlurJobState.Completed or BlurJobState.Failed)
        {
            return false;
        }

        job.State = BlurJobState.Running;
        job.Progress = progress;
        job.Stage = stage;
        job.Message = message;
        return true;
    }

    public bool TryComplete(string jobId, BlurDetectionMetricsDto result)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return false;
        }

        job.State = BlurJobState.Completed;
        job.Progress = 100;
        job.Stage = "completed";
        job.Message = "Processing complete";
        job.CompletedAt = DateTime.UtcNow;
        job.Result = result;
        return true;
    }

    public bool TryFail(string jobId, string error, bool canceled)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return false;
        }

        job.State = canceled ? BlurJobState.Canceled : BlurJobState.Failed;
        job.Progress = 100;
        job.Stage = canceled ? "canceled" : "failed";
        job.Message = canceled ? "Job canceled" : "Processing failed";
        job.Error = error;
        job.CompletedAt = DateTime.UtcNow;
        return true;
    }

    private void CleanupExpiredJobs()
    {
        var threshold = DateTime.UtcNow - JobRetention;
        foreach (var pair in _jobs)
        {
            var job = pair.Value;
            if (job.CompletedAt is not null && job.CompletedAt < threshold)
            {
                job.State = BlurJobState.Expired;
                _jobs.TryRemove(pair.Key, out _);
                job.Cancellation.Dispose();
            }
        }
    }

    private static BlurJobUpdateDto ToDto(BlurJob job)
    {
        return new BlurJobUpdateDto
        {
            JobId = job.Id,
            Status = job.State.ToString(),
            Progress = job.Progress,
            Stage = job.Stage,
            Message = job.Message,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            Error = job.Error,
            Result = job.Result
        };
    }
}
