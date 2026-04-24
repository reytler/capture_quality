namespace CaptureQuality.Services;

public class ConfigurationService
{
    public int K { get; set; } = 1;
    public int PatchSize { get; set; } = 27;
    public double PatchThreshold { get; set; } = 0.64;
    public double BlurRatioThreshold { get; set; } = 0.35;
    public int MedianFilterSize { get; set; } = 31;
    public int GradientKernelSize { get; set; } = 3;
    public int MaxImageDimension { get; set; } = 1200;
}