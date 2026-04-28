namespace CaptureQuality.Server.Configuration;

public sealed class ServerBlurDetectionOptions
{
    public const string SectionName = "BlurDetection";

    public BlurDetectionEngineKind Engine { get; set; } = BlurDetectionEngineKind.Legacy;
    public OpenCvSpikeOptions OpenCvSpike { get; set; } = new();
}

public sealed class OpenCvSpikeOptions
{
    public int PatchSize { get; set; } = 40;
    public int MaxImageDimension { get; set; } = 1600;
    public double MinUsefulContentRatio { get; set; } = 0.12;
    public double MinUsefulImageRatio { get; set; } = 0.02;
    public double BlurVarianceThreshold { get; set; } = 140;
    public double BlurRatioThreshold { get; set; } = 0.35;
    public int MinEligiblePatchCount { get; set; } = 6;
    public double MinEligiblePatchRatio { get; set; } = 0.05;
    public double MinMedianVariance { get; set; } = 160;
    public int PreBlurKernelSize { get; set; } = 3;
    public int AdaptiveThresholdBlockSize { get; set; } = 31;
    public double AdaptiveThresholdC { get; set; } = 15;
    public int MorphologyKernelSize { get; set; } = 5;
}
