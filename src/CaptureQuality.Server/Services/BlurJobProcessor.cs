using CaptureQuality.Models;
using CaptureQuality.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CaptureQuality.Server.Services;

public sealed class BlurJobProcessor : BackgroundService
{
    private readonly IBlurJobStore _jobStore;
    private readonly IBlurDetectionEngine _detector;
    private readonly IHubContext<BlurJobHub> _hubContext;
    private readonly ILogger<BlurJobProcessor> _logger;

    public BlurJobProcessor(
        IBlurJobStore jobStore,
        IBlurDetectionEngine detector,
        IHubContext<BlurJobHub> hubContext,
        ILogger<BlurJobProcessor> logger)
    {
        _jobStore = jobStore;
        _detector = detector;
        _hubContext = hubContext;
        _logger = logger;
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
                _logger.LogInformation("Starting blur job {JobId} with engine {EngineName}", jobId, _detector.Name);
                using var stream = new MemoryStream(job.ImageBytes, writable: false);
                var result = await _detector.DetectBlurAsync(
                    stream,
                    progress => HandleProgress(jobId, progress),
                    linkedCts.Token);

                _jobStore.TryComplete(jobId, result.ToDto());
                _logger.LogInformation("Completed blur job {JobId} with engine {EngineName} and status {Status}", jobId, _detector.Name, result.Status);
                await PublishAsync(jobId, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Canceled blur job {JobId} with engine {EngineName}", jobId, _detector.Name);
                _jobStore.TryFail(jobId, "Job canceled", canceled: true);
                await PublishAsync(jobId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Blur job {JobId} failed with engine {EngineName}", jobId, _detector.Name);
                _jobStore.TryFail(jobId, ex.Message, canceled: false);
                await PublishAsync(jobId, stoppingToken);
            }
        }
    }

    private async Task HandleProgress(string jobId, int progress)
    {
        var (stage, message) = MapStage(progress);
        _jobStore.TryMarkProgress(jobId, progress, stage, message);
        await PublishAsync(jobId, CancellationToken.None);
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
            10 => ("loading", "Image loaded"),
            15 => ("resize", "Image resized"),
            20 => ("grayscale", "Grayscale conversion complete"),
            25 => ("median-filter", "Median filter applied"),
            30 => ("patch-analysis", "Patch analysis started"),
            45 => ("patch-analysis", "Patch analysis complete"),
            50 => ("foreground-filter", "Foreground filtering complete"),
            65 => ("global-ratio", "Global blur ratio calculated"),
            80 => ("finalizing", "Finalizing results"),
            100 => ("completed", "Processing complete"),
            _ when progress > 30 && progress < 45 => ("patch-analysis", "Analyzing patches"),
            _ => ("running", "Processing image")
        };
    }
}
