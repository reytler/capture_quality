using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace CaptureQuality.Services;

public class SvdAnalyzerService
{
    private readonly ConfigurationService _config;

    public SvdAnalyzerService(ConfigurationService config)
    {
        _config = config;
    }

    public double CalculateBk(float[,] patch, int k)
    {
        int rows = patch.GetLength(0);
        int cols = patch.GetLength(1);

        var matrix = Matrix<double>.Build.Dense(rows, cols);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                matrix[x, y] = patch[x, y];
            }
        }

        var svd = matrix.Svd(true);
        var singularValues = svd.S;
        double sumAll = 0;
        double sumFirstK = 0;

        for (int i = 0; i < singularValues.Count; i++)
        {
            sumAll += singularValues[i] * singularValues[i];
            if (i < k)
            {
                sumFirstK += singularValues[i] * singularValues[i];
            }
        }

        if (sumAll < 1e-10)
            return 0;

        return sumFirstK / sumAll;
    }

    public async Task<List<(int x, int y, double bk)>> AnalyzePatches(
        float[,] foreground,
        int patchSize,
        int k,
        Func<double, Task>? onProgress = null)
    {
        int width = foreground.GetLength(0);
        int height = foreground.GetLength(1);
        var results = new List<(int x, int y, double bk)>();

        int totalPatchesY = (height - patchSize) / patchSize + 1;
        int totalPatchesX = (width - patchSize) / patchSize + 1;
        int totalPatches = totalPatchesY * totalPatchesX;
        int patchCount = 0;

        for (int py = 0; py <= height - patchSize; py += patchSize)
        {
            for (int px = 0; px <= width - patchSize; px += patchSize)
            {
                var patch = new float[patchSize, patchSize];
                for (int y = 0; y < patchSize; y++)
                {
                    for (int x = 0; x < patchSize; x++)
                    {
                        if (px + x < width && py + y < height)
                        {
                            patch[x, y] = foreground[px + x, py + y];
                        }
                    }
                }

                double bk = CalculateBk(patch, k);
                results.Add((px, py, bk));

                patchCount++;
                if (onProgress != null && patchCount % Math.Max(1, totalPatches / 10) == 0)
                {
                    await onProgress((double)patchCount / totalPatches);
                }
            }
        }

        if (onProgress != null)
        {
            await onProgress(1.0);
        }

        // Calculate Bk statistics for diagnostic
        if (results.Any())
        {
            var bkValues = results.Select(r => r.bk).OrderBy(b => b).ToList();
            double bkMin = bkValues.First();
            double bkMax = bkValues.Last();
            double bkMedian = bkValues[bkValues.Count / 2];
            double bkMean = bkValues.Average();
            int highBkCount = bkValues.Count(v => v >= _config.PatchThreshold);
            
            Console.WriteLine($"[SvdAnalyzer:AnalyzePatches] Patch analysis complete - Total: {results.Count}");
            Console.WriteLine($"[SvdAnalyzer:AnalyzePatches] Bk stats - Min: {bkMin:F4}, Max: {bkMax:F4}, Median: {bkMedian:F4}, Mean: {bkMean:F4}");
            Console.WriteLine($"[SvdAnalyzer:AnalyzePatches] Patches with Bk >= {_config.PatchThreshold}: {highBkCount} ({(highBkCount * 100.0 / results.Count):F2}%)");
        }

        return results;
    }

    public (int totalPatches, int blurredPatches, double blurRatio, bool[,] blurMap) CalculateGlobalBlurRatio(
        List<(int x, int y, double bk)> patchResults,
        int width,
        int height,
        int patchSize,
        double threshold)
    {
        int mapWidth = (width + patchSize - 1) / patchSize;
        int mapHeight = (height + patchSize - 1) / patchSize;
        var blurMap = new bool[mapWidth, mapHeight];

        int blurredCount = 0;
        foreach (var result in patchResults)
        {
            int mx = result.x / patchSize;
            int my = result.y / patchSize;
            bool isBlurred = result.bk >= threshold;
            blurMap[mx, my] = isBlurred;
            if (isBlurred) blurredCount++;
        }

        int total = patchResults.Count;
        double blurRatio = total > 0 ? (double)blurredCount / total : 0;

        Console.WriteLine($"[SvdAnalyzer:CalculateGlobalBlurRatio] blurRatio: {blurRatio:F4}, threshold: {threshold}");
        Console.WriteLine($"[SvdAnalyzer:CalculateGlobalBlurRatio] Total patches: {total}, Blurred: {blurredCount}, Clear: {total - blurredCount}");
        Console.WriteLine($"[SvdAnalyzer:CalculateGlobalBlurRatio] Decision: {(blurRatio >= threshold ? "REJECTED" : "ACCEPTED")}");

        return (total, blurredCount, blurRatio, blurMap);
    }
}