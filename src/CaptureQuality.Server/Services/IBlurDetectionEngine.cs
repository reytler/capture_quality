using CaptureQuality.Models;

namespace CaptureQuality.Server.Services;

public interface IBlurDetectionEngine
{
    string Name { get; }

    Task<BlurDetectionResult> DetectBlurAsync(
        Stream imageStream,
        Func<int, Task>? onProgress = null,
        CancellationToken cancellationToken = default);
}
