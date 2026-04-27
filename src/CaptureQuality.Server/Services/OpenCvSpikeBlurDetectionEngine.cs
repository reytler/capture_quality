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

        int patchSize = Math.Max(8, options.PatchSize);
        int patchColumns = (grayscale.Width + patchSize - 1) / patchSize;
        int patchRows = (grayscale.Height + patchSize - 1) / patchSize;
        int totalPatches = patchColumns * patchRows;

        if (totalPatches == 0)
        {
            return new BlurDetectionResult
            {
                IsAccepted = true,
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

                double variance = CalculateLaplacianVariance(patch);
                bool isBlurred = variance < options.BlurVarianceThreshold;
                blurMap[patchColumn, patchRow] = isBlurred;
                if (isBlurred)
                {
                    blurredPatches++;
                }

                processedPatches++;
                int progress = 30 + (int)Math.Round(processedPatches * 35d / totalPatches);
                await (onProgress?.Invoke(Math.Min(progress, 65)) ?? Task.CompletedTask);
            }
        }

        double blurRatio = totalPatches > 0 ? (double)blurredPatches / totalPatches : 0;
        bool isAccepted = blurRatio < options.BlurRatioThreshold;

        await (onProgress?.Invoke(80) ?? Task.CompletedTask);
        await (onProgress?.Invoke(100) ?? Task.CompletedTask);

        return new BlurDetectionResult
        {
            IsAccepted = isAccepted,
            BlurRatio = blurRatio,
            TotalPatches = totalPatches,
            BlurredPatches = blurredPatches,
            Status = isAccepted ? "ACCEPTED" : "REJECTED",
            PatchSize = patchSize,
            ImageWidth = grayscale.Width,
            ImageHeight = grayscale.Height,
            BlurMap = blurMap
        };
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
}
