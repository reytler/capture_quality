using CaptureQuality.Models;
using CaptureQuality.Server.Configuration;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace CaptureQuality.Server.Services;

public sealed class OpenCvSpikeBlurDetectionEngine : IBlurDetectionEngine
{
    private readonly IOptionsMonitor<ServerBlurDetectionOptions> _optionsMonitor;

    public string Name => "OpenCvSpike";

    public OpenCvSpikeBlurDetectionEngine(IOptionsMonitor<ServerBlurDetectionOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    public async Task<BlurDetectionResult> DetectBlurAsync(
        Stream imageStream,
        Func<int, Task>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue.OpenCvSpike;

        using var buffer = new MemoryStream();
        await imageStream.CopyToAsync(buffer, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        using var image = Cv2.ImDecode(buffer.ToArray(), ImreadModes.Color);
        if (image.Empty())
        {
            throw new BadHttpRequestException("Unable to decode image", StatusCodes.Status400BadRequest);
        }

        await (onProgress?.Invoke(10) ?? Task.CompletedTask);

        using var resized = ResizeIfNeeded(image, options.MaxImageDimension);
        await (onProgress?.Invoke(15) ?? Task.CompletedTask);
        cancellationToken.ThrowIfCancellationRequested();

        using var grayscale = new Mat();
        Cv2.CvtColor(resized, grayscale, ColorConversionCodes.BGR2GRAY);
        await (onProgress?.Invoke(20) ?? Task.CompletedTask);
        cancellationToken.ThrowIfCancellationRequested();

        using var usefulContentMask = BuildUsefulContentMask(grayscale, options);
        await (onProgress?.Invoke(25) ?? Task.CompletedTask);
        cancellationToken.ThrowIfCancellationRequested();

        int usefulContentPixels = Cv2.CountNonZero(usefulContentMask);
        double usefulImageRatio = grayscale.Width > 0 && grayscale.Height > 0
            ? usefulContentPixels / (double)(grayscale.Width * grayscale.Height)
            : 0;

        int patchSize = Math.Max(8, options.PatchSize);
        int patchColumns = (grayscale.Width + patchSize - 1) / patchSize;
        int patchRows = (grayscale.Height + patchSize - 1) / patchSize;
        int gridPatches = patchColumns * patchRows;

        if (gridPatches == 0)
        {
            return new BlurDetectionResult
            {
                IsAccepted = false,
                BlurRatio = 0,
                TotalPatches = 0,
                BlurredPatches = 0,
                Status = "NO_CONTENT",
                PatchSize = patchSize,
                ImageWidth = grayscale.Width,
                ImageHeight = grayscale.Height,
                BlurMap = new bool[0, 0]
            };
        }

        await (onProgress?.Invoke(30) ?? Task.CompletedTask);

        var blurMap = new bool[patchColumns, patchRows];
        var eligibleVariances = new List<double>();
        int eligiblePatches = 0;
        int blurredPatches = 0;
        int processedPatches = 0;

        for (int patchRow = 0; patchRow < patchRows; patchRow++)
        {
            for (int patchColumn = 0; patchColumn < patchColumns; patchColumn++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int x = patchColumn * patchSize;
                int y = patchRow * patchSize;
                int width = Math.Min(patchSize, grayscale.Width - x);
                int height = Math.Min(patchSize, grayscale.Height - y);

                var roi = new Rect(x, y, width, height);
                using var patch = new Mat(grayscale, roi);
                using var maskPatch = new Mat(usefulContentMask, roi);

                int patchArea = width * height;
                double contentRatio = patchArea > 0
                    ? Cv2.CountNonZero(maskPatch) / (double)patchArea
                    : 0;

                if (contentRatio >= options.MinUsefulContentRatio)
                {
                    double variance = CalculateLaplacianVariance(patch);
                    bool isBlurred = variance < options.BlurVarianceThreshold;
                    blurMap[patchColumn, patchRow] = isBlurred;
                    eligibleVariances.Add(variance);
                    eligiblePatches++;

                    if (isBlurred)
                    {
                        blurredPatches++;
                    }
                }

                processedPatches++;
                int progress = 30 + (int)Math.Round(processedPatches * 35d / gridPatches);
                await (onProgress?.Invoke(Math.Min(progress, 65)) ?? Task.CompletedTask);
            }
        }

        if (usefulImageRatio < options.MinUsefulImageRatio)
        {
            Console.WriteLine(
                $"[OpenCvSpikeBlurDetectionEngine] Useful ROI too small. Image: {grayscale.Width}x{grayscale.Height}, UsefulImageRatio: {usefulImageRatio:F4}, Threshold: {options.MinUsefulImageRatio:F4}");

            await (onProgress?.Invoke(80) ?? Task.CompletedTask);
            await (onProgress?.Invoke(100) ?? Task.CompletedTask);

            return new BlurDetectionResult
            {
                IsAccepted = false,
                BlurRatio = 0,
                TotalPatches = eligiblePatches,
                BlurredPatches = blurredPatches,
                Status = "NO_CONTENT",
                PatchSize = patchSize,
                ImageWidth = grayscale.Width,
                ImageHeight = grayscale.Height,
                BlurMap = blurMap
            };
        }

        if (eligiblePatches == 0)
        {
            Console.WriteLine($"[OpenCvSpikeBlurDetectionEngine] No useful content patches found. Image: {grayscale.Width}x{grayscale.Height}, PatchSize: {patchSize}");

            await (onProgress?.Invoke(80) ?? Task.CompletedTask);
            await (onProgress?.Invoke(100) ?? Task.CompletedTask);

            return new BlurDetectionResult
            {
                IsAccepted = false,
                BlurRatio = 0,
                TotalPatches = 0,
                BlurredPatches = 0,
                Status = "NO_CONTENT",
                PatchSize = patchSize,
                ImageWidth = grayscale.Width,
                ImageHeight = grayscale.Height,
                BlurMap = blurMap
            };
        }

        double eligiblePatchRatio = eligiblePatches / (double)gridPatches;
        if (eligiblePatches < options.MinEligiblePatchCount || eligiblePatchRatio < options.MinEligiblePatchRatio)
        {
            Console.WriteLine(
                $"[OpenCvSpikeBlurDetectionEngine] Insufficient eligible patches. EligiblePatches: {eligiblePatches}/{gridPatches}, EligiblePatchRatio: {eligiblePatchRatio:F4}, Thresholds: Count >= {options.MinEligiblePatchCount}, Ratio >= {options.MinEligiblePatchRatio:F4}");

            await (onProgress?.Invoke(80) ?? Task.CompletedTask);
            await (onProgress?.Invoke(100) ?? Task.CompletedTask);

            return new BlurDetectionResult
            {
                IsAccepted = false,
                BlurRatio = eligiblePatches > 0 ? blurredPatches / (double)eligiblePatches : 0,
                TotalPatches = eligiblePatches,
                BlurredPatches = blurredPatches,
                Status = "INSUFFICIENT_CONTENT",
                PatchSize = patchSize,
                ImageWidth = grayscale.Width,
                ImageHeight = grayscale.Height,
                BlurMap = blurMap
            };
        }

        double blurRatio = blurredPatches / (double)eligiblePatches;
        double medianVariance = CalculateMedian(eligibleVariances);
        bool isAccepted = blurRatio < options.BlurRatioThreshold
            && medianVariance >= options.MinMedianVariance;

        Console.WriteLine(
            $"[OpenCvSpikeBlurDetectionEngine] Image: {grayscale.Width}x{grayscale.Height}, PatchSize: {patchSize}, UsefulImageRatio: {usefulImageRatio:F4}, EligiblePatches: {eligiblePatches}/{gridPatches}, BlurredPatches: {blurredPatches}, BlurRatio: {blurRatio:F4}, MedianVariance: {medianVariance:F2}");
        Console.WriteLine(
            $"[OpenCvSpikeBlurDetectionEngine] Thresholds - ContentRatio: {options.MinUsefulContentRatio:F2}, UsefulImageRatio: {options.MinUsefulImageRatio:F4}, BlurVariance: {options.BlurVarianceThreshold:F2}, BlurRatio: {options.BlurRatioThreshold:F2}, MinEligiblePatches: {options.MinEligiblePatchCount}, MinEligiblePatchRatio: {options.MinEligiblePatchRatio:F4}, MinMedianVariance: {options.MinMedianVariance:F2}");
        Console.WriteLine(
            $"[OpenCvSpikeBlurDetectionEngine] Decision: {(isAccepted ? "ACCEPTED" : "REJECTED")}");

        await (onProgress?.Invoke(80) ?? Task.CompletedTask);
        await (onProgress?.Invoke(100) ?? Task.CompletedTask);

        return new BlurDetectionResult
        {
            IsAccepted = isAccepted,
            BlurRatio = blurRatio,
            TotalPatches = eligiblePatches,
            BlurredPatches = blurredPatches,
            Status = isAccepted ? "ACCEPTED" : "REJECTED",
            PatchSize = patchSize,
            ImageWidth = grayscale.Width,
            ImageHeight = grayscale.Height,
            BlurMap = blurMap
        };
    }

    private static Mat BuildUsefulContentMask(Mat grayscale, OpenCvSpikeOptions options)
    {
        int preBlurKernelSize = NormalizeKernelSize(options.PreBlurKernelSize, minimumOddValue: 1);
        int adaptiveBlockSize = NormalizeKernelSize(options.AdaptiveThresholdBlockSize, minimumOddValue: 3);
        int morphologyKernelSize = NormalizeKernelSize(options.MorphologyKernelSize, minimumOddValue: 1);

        using var prepared = new Mat();
        if (preBlurKernelSize > 1)
        {
            Cv2.GaussianBlur(grayscale, prepared, new Size(preBlurKernelSize, preBlurKernelSize), 0);
        }
        else
        {
            grayscale.CopyTo(prepared);
        }

        var mask = new Mat();
        Cv2.AdaptiveThreshold(
            prepared,
            mask,
            255,
            AdaptiveThresholdTypes.GaussianC,
            ThresholdTypes.BinaryInv,
            adaptiveBlockSize,
            options.AdaptiveThresholdC);

        if (morphologyKernelSize > 1)
        {
            using var kernel = Cv2.GetStructuringElement(
                MorphShapes.Rect,
                new Size(morphologyKernelSize, morphologyKernelSize));
            Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);
        }

        return mask;
    }

    private static Mat ResizeIfNeeded(Mat image, int maxDimension)
    {
        if (image.Width <= maxDimension && image.Height <= maxDimension)
        {
            return image.Clone();
        }

        double scale = image.Width > image.Height
            ? maxDimension / (double)image.Width
            : maxDimension / (double)image.Height;

        int targetWidth = Math.Max(1, (int)Math.Round(image.Width * scale));
        int targetHeight = Math.Max(1, (int)Math.Round(image.Height * scale));

        var resized = new Mat();
        Cv2.Resize(image, resized, new Size(targetWidth, targetHeight), interpolation: InterpolationFlags.Area);
        return resized;
    }

    private static double CalculateLaplacianVariance(Mat patch)
    {
        using var laplacian = new Mat();
        Cv2.Laplacian(patch, laplacian, MatType.CV_64F);
        Cv2.MeanStdDev(laplacian, out _, out var stddev);
        double sigma = stddev.Val0;
        return sigma * sigma;
    }

    private static double CalculateMedian(List<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        values.Sort();
        int middleIndex = values.Count / 2;

        if (values.Count % 2 == 0)
        {
            return (values[middleIndex - 1] + values[middleIndex]) / 2d;
        }

        return values[middleIndex];
    }

    private static int NormalizeKernelSize(int value, int minimumOddValue)
    {
        int normalized = Math.Max(minimumOddValue, value);
        return normalized % 2 == 0 ? normalized + 1 : normalized;
    }
}
