namespace CaptureQuality.Server.Configuration;

public sealed class ServerBlurDetectionOptions
{
    public const string SectionName = "BlurDetection";

    public BlurDetectionEngineKind Engine { get; set; } = BlurDetectionEngineKind.Legacy;
    public OpenCvSpikeOptions OpenCvSpike { get; set; } = new();
}

public sealed class OpenCvSpikeOptions
{
    public int PatchSize { get; set; } = 64;
    public int MaxImageDimension { get; set; } = 1200;
    public double BlurVarianceThreshold { get; set; } = 140;
    public double BlurRatioThreshold { get; set; } = 0.40;
}
