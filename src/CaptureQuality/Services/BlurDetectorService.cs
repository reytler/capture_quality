using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using CaptureQuality.Models;

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

    public async Task<BlurDetectionResult> DetectBlurAsync(
        Stream imageStream,
        Func<int, Task>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[BlurDetectorService:DetectBlurAsync] ENTRY");

        using var image = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken);
        Console.WriteLine($"[BlurDetectorService:DetectBlurAsync] Image loaded: {image.Width}x{image.Height}");
        await (onProgress?.Invoke(10) ?? Task.CompletedTask);
        cancellationToken.ThrowIfCancellationRequested();

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
            Console.WriteLine($"[BlurDetectorService:DetectBlurAsync] Image resized to: {image.Width}x{image.Height} (original: {image.Width}x{image.Height})");
        }

        await (onProgress?.Invoke(15) ?? Task.CompletedTask);
        cancellationToken.ThrowIfCancellationRequested();

        var grayscale = _imageProcessor.ToGrayscale(image);
        // Log grayscale stats
        double grayMean = 0;
        int grayWidth = grayscale.GetLength(0);
        int grayHeight = grayscale.GetLength(1);
        for (int y = 0; y < grayHeight; y++)
            for (int x = 0; x < grayWidth; x++)
                grayMean += grayscale[x, y];
        grayMean /= (grayWidth * grayHeight);
        Console.WriteLine($"[BlurDetectorService:DetectBlurAsync] Grayscale stats - Mean: {grayMean:F4}, Dimensions: {grayWidth}x{grayHeight}");
        await (onProgress?.Invoke(20) ?? Task.CompletedTask);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Console.WriteLine($"[BlurDetectorService:DetectBlurAsync] Running in background thread...");

            var medianFiltered = _imageProcessor.ApplyMedianFilter(grayscale, _config.MedianFilterSize);
            Console.WriteLine($"[BlurDetectorService:DetectBlurAsync] ApplyMedianFilter done");
            await (onProgress?.Invoke(25) ?? Task.CompletedTask);
            cancellationToken.ThrowIfCancellationRequested();

            var (intensity, localMedian, gradientMagnitude) = _imageProcessor.ExtractFeatures(grayscale, medianFiltered);
            Console.WriteLine($"[BlurDetectorService:DetectBlurAsync] ExtractFeatures done");
            cancellationToken.ThrowIfCancellationRequested();

            var (segmentation, centroids) = _imageProcessor.ApplyKmeans(intensity, localMedian, gradientMagnitude);
            Console.WriteLine($"[BlurDetectorService:DetectBlurAsync] ApplyKmeans done");
            
            // Log K-means centroids and cluster distribution
            int cluster0Count = 0, cluster1Count = 0;
            int segWidth = segmentation.GetLength(0);
            int segHeight = segmentation.GetLength(1);
            for (int y = 0; y < segHeight; y++)
                for (int x = 0; x < segWidth; x++)
                    if (segmentation[x, y] == 0) cluster0Count++; else cluster1Count++;
            
            int fgLabel = centroids[0, 2] > centroids[1, 2] ? 0 : 1;
            Console.WriteLine($"[BlurDetectorService:DetectBlurAsync] K-means - Cluster0: {cluster0Count}, Cluster1: {cluster1Count}, fgLabel: {fgLabel}");
            Console.WriteLine($"[BlurDetectorService:DetectBlurAsync] Centroids - C0: [I={centroids[0,0]:F4}, M={centroids[0,1]:F4}, G={centroids[0,2]:F4}], C1: [I={centroids[1,0]:F4}, M={centroids[1,1]:F4}, G={centroids[1,2]:F4}]");

            cancellationToken.ThrowIfCancellationRequested();

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
                Console.WriteLine($"[BlurDetectorService:DetectBlurAsync] NO CONTENT - fgCount: 0");
                return new BlurDetectionResult
                {
                    IsAccepted = true,
                    BlurRatio = 0,
                    TotalPatches = 0,
                    BlurredPatches = 0,
                    Status = "NO_CONTENT"
                };
            }

            Console.WriteLine($"[BlurDetectorService:DetectBlurAsync] Foreground extraction done - fgCount: {fgCount}, fgPercentage: {fgCount * 100.0 / (grayscale.GetLength(0) * grayscale.GetLength(1)):F2}%, dimensions: {grayscale.GetLength(0)}x{grayscale.GetLength(1)}");

            var patchResults = await _svdAnalyzer.AnalyzePatches(
                foreground,
                _config.PatchSize,
                _config.K,
                progress => (onProgress?.Invoke(30 + (int)(progress * 15)) ?? Task.CompletedTask));
            Console.WriteLine($"[BlurDetectorService:DetectBlurAsync] AnalyzePatches done - {patchResults.Count} patches");

            await (onProgress?.Invoke(50) ?? Task.CompletedTask);
            cancellationToken.ThrowIfCancellationRequested();

            var fgPatchCount = 0;
            var fgPatches = new List<(int x, int y, double bk)>();
            foreach (var patchResult in patchResults)
            {
                bool hasFg = false;
                int ps = _config.PatchSize;
                for (int py = 0; py < ps && !hasFg; py++)
                {
                    for (int px = 0; px < ps && !hasFg; px++)
                    {
                        int nx = patchResult.x + px;
                        int ny = patchResult.y + py;
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
                    fgPatches.Add(patchResult);
                    fgPatchCount++;
                }
            }

            await (onProgress?.Invoke(65) ?? Task.CompletedTask);
            cancellationToken.ThrowIfCancellationRequested();

            int width = grayscale.GetLength(0);
            int height = grayscale.GetLength(1);
            var (total, blurred, blurRatio, blurMap) = _svdAnalyzer.CalculateGlobalBlurRatio(
                fgPatches, width, height, _config.PatchSize, _config.PatchThreshold);
            Console.WriteLine($"[BlurDetectorService:DetectBlurAsync] CalculateGlobalBlurRatio done - blurRatio: {blurRatio:F4}");

            await (onProgress?.Invoke(80) ?? Task.CompletedTask);
            cancellationToken.ThrowIfCancellationRequested();

            bool isAccepted = blurRatio < _config.BlurRatioThreshold;

            // Summary log for diagnostic
            Console.WriteLine($"[BlurDetectorService] === ANALYSIS SUMMARY ===");
            Console.WriteLine($"[BlurDetectorService] Image: {width}x{height}, PatchSize: {_config.PatchSize}, K: {_config.K}");
            Console.WriteLine($"[BlurDetectorService] Thresholds - PatchThreshold: {_config.PatchThreshold}, BlurRatioThreshold: {_config.BlurRatioThreshold}");
            Console.WriteLine($"[BlurDetectorService] Filters - MedianSize: {_config.MedianFilterSize}, GradientKernel: {_config.GradientKernelSize}");
            Console.WriteLine($"[BlurDetectorService] Results - TotalPatches: {total}, BlurredPatches: {blurred}, BlurRatio: {blurRatio:F4}");
            Console.WriteLine($"[BlurDetectorService] Decision: {(isAccepted ? "ACCEPTED" : "REJECTED")} (BlurRatio {blurRatio:F4} < Threshold {_config.BlurRatioThreshold})");
            Console.WriteLine($"[BlurDetectorService] =========================");

            Console.WriteLine($"[BlurDetectorService:DetectBlurAsync] DONE - IsAccepted: {isAccepted}");

            await (onProgress?.Invoke(100) ?? Task.CompletedTask);

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
        }, cancellationToken);

        return result;
    }
}
