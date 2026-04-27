using CaptureQuality.Models;
using CaptureQuality.Services;

namespace CaptureQuality.Server.Services;

public sealed class LegacySvdBlurDetectionEngine : IBlurDetectionEngine
{
    private readonly BlurDetectorService _blurDetectorService;

    public string Name => "LegacySvd";

    public LegacySvdBlurDetectionEngine(BlurDetectorService blurDetectorService)
    {
        _blurDetectorService = blurDetectorService;
    }

    public Task<BlurDetectionResult> DetectBlurAsync(
        Stream imageStream,
        Func<int, Task>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        return _blurDetectorService.DetectBlurAsync(imageStream, onProgress, cancellationToken);
    }
}
