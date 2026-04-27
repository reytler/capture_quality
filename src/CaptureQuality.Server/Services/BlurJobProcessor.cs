using CaptureQuality.Models;
using CaptureQuality.Server.Hubs;
using CaptureQuality.Services;
using Microsoft.AspNetCore.SignalR;

namespace CaptureQuality.Server.Services;

public sealed class BlurJobProcessor : BackgroundService
{
    private readonly IBlurJobStore _jobStore;
    private readonly BlurDetectorService _detector;
    private readonly IHubContext<BlurJobHub> _hubContext;

    public BlurJobProcessor(
        IBlurJobStore jobStore,
        BlurDetectorService detector,
        IHubContext<BlurJobHub> hubContext)
    {
        _jobStore = jobStore;
        _detector = detector;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var jobId in _jobStore.QueueReader.ReadAllAsync(stoppingToken))
        {
            var job = _jobStore.GetJob(jobId);
            if (job is null)
            {
                continue;
            }

            if (!_jobStore.TryMarkRunning(jobId))
            {
                await PublishAsync(jobId, stoppingToken);
                continue;
            }

            await PublishAsync(jobId, stoppingToken);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, job.Cancellation.Token);

            try
            {
                using var stream = new MemoryStream(job.ImageBytes, writable: false);
                var result = await _detector.DetectBlurAsync(
                    stream,
                    progress => HandleProgress(jobId, progress),
                    linkedCts.Token);

                var dto = new BlurDetectionMetricsDto
                {
                    IsAccepted = result.IsAccepted,
                    BlurRatio = result.BlurRatio,
                    TotalPatches = result.TotalPatches,
                    BlurredPatches = result.BlurredPatches,
                    Status = result.Status,
                    PatchSize = result.PatchSize,
                    ImageWidth = result.ImageWidth,
                    ImageHeight = result.ImageHeight
                };

                _jobStore.TryComplete(jobId, dto);
                await PublishAsync(jobId, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _jobStore.TryFail(jobId, "Job canceled", canceled: true);
                await PublishAsync(jobId, stoppingToken);
            }
            catch (Exception ex)
            {
                _jobStore.TryFail(jobId, ex.Message, canceled: false);
                await PublishAsync(jobId, stoppingToken);
            }
        }
    }

    private void HandleProgress(string jobId, int progress)
    {
        var (stage, message) = MapStage(progress);
        _jobStore.TryMarkProgress(jobId, progress, stage, message);
        _ = PublishAsync(jobId, CancellationToken.None);
    }

    private async Task PublishAsync(string jobId, CancellationToken cancellationToken)
    {
        var update = _jobStore.Get(jobId);
        if (update is null)
        {
            return;
        }

        await _hubContext.Clients.Group(BlurJobHub.GetGroupName(jobId)).SendAsync("JobUpdated", update, cancellationToken);
    }

    private static (string Stage, string Message) MapStage(int progress)
    {
        return progress switch
        {
            25 => ("segmentation", "Segmentation complete"),
            50 => ("patch-analysis", "Patch analysis complete"),
            75 => ("foreground-filter", "Foreground filtering complete"),
            100 => ("completed", "Processing complete"),
            _ => ("running", "Processing image")
        };
    }
}
