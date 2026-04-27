using System.Threading.Channels;
using CaptureQuality.Models;

namespace CaptureQuality.Server.Services;

public interface IBlurJobStore
{
    ChannelReader<string> QueueReader { get; }
    Task<CreateBlurJobResponse> EnqueueAsync(string fileName, string contentType, byte[] imageBytes, CancellationToken cancellationToken);
    BlurJobUpdateDto? Get(string jobId);
    BlurJob? GetJob(string jobId);
    bool TrySubscribe(string jobId);
    bool TryCancel(string jobId);
    bool TryMarkRunning(string jobId);
    bool TryMarkProgress(string jobId, int progress, string stage, string message);
    bool TryComplete(string jobId, BlurDetectionMetricsDto result);
    bool TryFail(string jobId, string error, bool canceled);
}
