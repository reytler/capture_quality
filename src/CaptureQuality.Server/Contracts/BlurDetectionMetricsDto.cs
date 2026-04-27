namespace CaptureQuality.Server.Contracts;

public sealed class BlurDetectionMetricsDto
{
    public bool IsAccepted { get; init; }
    public double BlurRatio { get; init; }
    public int TotalPatches { get; init; }
    public int BlurredPatches { get; init; }
    public string Status { get; init; } = "";
    public int PatchSize { get; init; }
    public int ImageWidth { get; init; }
    public int ImageHeight { get; init; }
}
