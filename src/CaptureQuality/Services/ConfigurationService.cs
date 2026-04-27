namespace CaptureQuality.Services;

public class ConfigurationService
{
    public int K { get; set; } = 1;
    public int PatchSize { get; set; } = 64;
    public double PatchThreshold { get; set; } = 0.72;
    public double BlurRatioThreshold { get; set; } = 0.40;
    public int MedianFilterSize { get; set; } = 24;
    public int GradientKernelSize { get; set; } = 3;
    public int MaxImageDimension { get; set; } = 1200;
}