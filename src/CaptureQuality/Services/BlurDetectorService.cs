using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CaptureQuality.Services;

public class BlurDetectorService
{
    private readonly ConfigurationService _config;
    private readonly ImageProcessorService _imageProcessor;
    private readonly SvdAnalyzerService _svdAnalyzer;

    public BlurDetectorService(
        ConfigurationService config,
        ImageProcessorService imageProcessor,
        SvdAnalyzerService svdAnalyzer)
    {
        _config = config;
        _imageProcessor = imageProcessor;
        _svdAnalyzer = svdAnalyzer;
    }

    public async Task<BlurDetectionResult> DetectBlurAsync(Stream imageStream)
    {
        using var image = await Image.LoadAsync<Rgba32>(imageStream);

        int maxDim = _config.MaxImageDimension;
        if (image.Width > maxDim || image.Height > maxDim)
        {
            int newWidth, newHeight;
            if (image.Width > image.Height)
            {
                newWidth = maxDim;
                newHeight = (int)((float)image.Height / image.Width * maxDim);
            }
            else
            {
                newHeight = maxDim;
                newWidth = (int)((float)image.Width / image.Height * maxDim);
            }
            image.Mutate(x => x.Resize(newWidth, newHeight));
        }

        var grayscale = _imageProcessor.ToGrayscale(image);
        
        var medianFiltered = _imageProcessor.ApplyMedianFilter(grayscale, _config.MedianFilterSize);
        
        var (intensity, localMedian, gradientMagnitude) = _imageProcessor.ExtractFeatures(grayscale, medianFiltered);
        
        var segmentation = _imageProcessor.ApplyKmeans(intensity, localMedian, gradientMagnitude);
        
        var foreground = new float[grayscale.GetLength(0), grayscale.GetLength(1)];
        int fgCount = 0;
        for (int y = 0; y < grayscale.GetLength(1); y++)
        {
            for (int x = 0; x < grayscale.GetLength(0); x++)
            {
                if (segmentation[x, y] == 1)
                {
                    foreground[x, y] = grayscale[x, y];
                    fgCount++;
                }
            }
        }
        
        if (fgCount == 0)
        {
            return new BlurDetectionResult
            {
                IsAccepted = true,
                BlurRatio = 0,
                TotalPatches = 0,
                BlurredPatches = 0,
                Status = "NO_CONTENT"
            };
        }

        var patchResults = _svdAnalyzer.AnalyzePatches(foreground, _config.PatchSize, _config.K);
        
        var fgPatchCount = 0;
        var fgPatches = new List<(int x, int y, double bk)>();
        foreach (var result in patchResults)
        {
            bool hasFg = false;
            int ps = _config.PatchSize;
            for (int py = 0; py < ps && !hasFg; py++)
            {
                for (int px = 0; px < ps && !hasFg; px++)
                {
                    int nx = result.x + px;
                    int ny = result.y + py;
                    if (nx < foreground.GetLength(0) && ny < foreground.GetLength(1))
                    {
                        if (foreground[nx, ny] > 0)
                        {
                            hasFg = true;
                        }
                    }
                }
            }
            if (hasFg)
            {
                fgPatches.Add(result);
                fgPatchCount++;
            }
        }

        int width = grayscale.GetLength(0);
        int height = grayscale.GetLength(1);
        var (total, blurred, blurRatio, blurMap) = _svdAnalyzer.CalculateGlobalBlurRatio(
            fgPatches, width, height, _config.PatchSize, _config.PatchThreshold);

        bool isAccepted = blurRatio < _config.BlurRatioThreshold;
        
        return new BlurDetectionResult
        {
            IsAccepted = isAccepted,
            BlurRatio = blurRatio,
            TotalPatches = total,
            BlurredPatches = blurred,
            Status = isAccepted ? "ACCEPTED" : "REJECTED",
            BlurMap = blurMap,
            PatchSize = _config.PatchSize,
            ImageWidth = width,
            ImageHeight = height
        };
    }
}

public class BlurDetectionResult
{
    public bool IsAccepted { get; set; }
    public double BlurRatio { get; set; }
    public int TotalPatches { get; set; }
    public int BlurredPatches { get; set; }
    public string Status { get; set; } = "";
    public bool[,]? BlurMap { get; set; }
    public int PatchSize { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
}