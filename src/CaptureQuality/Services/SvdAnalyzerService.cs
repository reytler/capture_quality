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

    public List<(int x, int y, double bk)> AnalyzePatches(
        float[,] foreground,
        int patchSize,
        int k)
    {
        int width = foreground.GetLength(0);
        int height = foreground.GetLength(1);
        var results = new List<(int x, int y, double bk)>();

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
            }
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

        return (total, blurredCount, blurRatio, blurMap);
    }
}