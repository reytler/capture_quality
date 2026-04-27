using CaptureQuality.Models;

namespace CaptureQuality.Server.Services;

public static class BlurDetectionResultMapper
{
    public static BlurDetectionMetricsDto ToDto(this BlurDetectionResult result)
    {
        return new BlurDetectionMetricsDto
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
    }
}
