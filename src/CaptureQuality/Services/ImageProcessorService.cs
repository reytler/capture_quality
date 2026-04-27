using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Memory;

namespace CaptureQuality.Services;

public class ImageProcessorService
{
    private readonly ConfigurationService _config;

    public ImageProcessorService(ConfigurationService config)
    {
        _config = config;
    }

    public float[,] ToGrayscale(Image<Rgba32> image)
    {
        int width = image.Width;
        int height = image.Height;
        var grayscale = new float[width, height];

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    var pixel = row[x];
                    grayscale[x, y] = (float)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B) / 255f;
                }
            }
        });

        return grayscale;
    }

    public float[,] ApplyMedianFilter(float[,] image, int size)
    {
        int width = image.GetLength(0);
        int height = image.GetLength(1);
        var result = new float[width, height];
        int halfSize = size / 2;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var values = new List<float>();
                for (int ky = -halfSize; ky <= halfSize; ky++)
                {
                    for (int kx = -halfSize; kx <= halfSize; kx++)
                    {
                        int nx = Math.Clamp(x + kx, 0, width - 1);
                        int ny = Math.Clamp(y + ky, 0, height - 1);
                        values.Add(image[nx, ny]);
                    }
                }
                values.Sort();
                result[x, y] = values[values.Count / 2];
            }
        }

        return result;
    }

    public (float[,] intensity, float[,] localMedian, float[,] gradientMagnitude) ExtractFeatures(
        float[,] grayscale, float[,] medianFiltered)
    {
        int width = grayscale.GetLength(0);
        int height = grayscale.GetLength(1);
        var intensity = new float[width, height];
        var localMedian = new float[width, height];
        var gradientMagnitude = new float[width, height];
        int medianWindow = 5;
        int halfMedian = medianWindow / 2;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                intensity[x, y] = grayscale[x, y];
                localMedian[x, y] = medianFiltered[x, y];
            }
        }

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                float gx = (grayscale[x + 1, y - 1] + 2 * grayscale[x + 1, y] + grayscale[x + 1, y + 1])
                        - (grayscale[x - 1, y - 1] + 2 * grayscale[x - 1, y] + grayscale[x - 1, y + 1]);
                float gy = (grayscale[x - 1, y + 1] + 2 * grayscale[x, y + 1] + grayscale[x + 1, y + 1])
                        - (grayscale[x - 1, y - 1] + 2 * grayscale[x, y - 1] + grayscale[x + 1, y - 1]);
                gradientMagnitude[x, y] = (float)Math.Sqrt(gx * gx + gy * gy);
            }
        }

        return (intensity, localMedian, gradientMagnitude);
    }

    public (int[,] labels, double[,] centroids) ApplyKmeans(float[,] intensity, float[,] localMedian, float[,] gradientMagnitude, int k = 2)
    {
        int width = intensity.GetLength(0);
        int height = intensity.GetLength(1);
        var labels = new int[width, height];

        var features = new float[width * height, 3];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                features[idx, 0] = intensity[x, y];
                features[idx, 1] = localMedian[x, y];
                features[idx, 2] = gradientMagnitude[x, y];
            }
        }

        var centroids = new double[k, 3];
        var random = new Random(42);
        for (int i = 0; i < k; i++)
        {
            centroids[i, 0] = random.NextDouble();
            centroids[i, 1] = random.NextDouble();
            centroids[i, 2] = random.NextDouble();
        }

        var assignments = new int[width * height];
        for (int iter = 0; iter < 50; iter++)
        {
            for (int i = 0; i < features.GetLength(0); i++)
            {
                double minDist = double.MaxValue;
                int minLabel = 0;
                for (int j = 0; j < k; j++)
                {
                    double dist = 0;
                    for (int d = 0; d < 3; d++)
                    {
                        double diff = features[i, d] - centroids[j, d];
                        dist += diff * diff;
                    }
                    if (dist < minDist)
                    {
                        minDist = dist;
                        minLabel = j;
                    }
                }
                assignments[i] = minLabel;
            }

            var newCentroids = new double[k, 3];
            var counts = new int[k];
            for (int i = 0; i < features.GetLength(0); i++)
            {
                int label = assignments[i];
                for (int d = 0; d < 3; d++)
                {
                    newCentroids[label, d] += features[i, d];
                }
                counts[label]++;
            }

            for (int j = 0; j < k; j++)
            {
                if (counts[j] > 0)
                {
                    for (int d = 0; d < 3; d++)
                    {
                        centroids[j, d] = newCentroids[j, d] / counts[j];
                    }
                }
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                labels[x, y] = assignments[y * width + x];
            }
        }

        int fgLabel = centroids[0, 2] > centroids[1, 2] ? 0 : 1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                labels[x, y] = labels[x, y] == fgLabel ? 1 : 0;
            }
        }

        return (labels, centroids);
    }

    public float[,] ResizeImage(float[,] image, int maxDimension)
    {
        int width = image.GetLength(0);
        int height = image.GetLength(1);

        if (width <= maxDimension && height <= maxDimension)
            return image;

        int newWidth, newHeight;
        if (width > height)
        {
            newWidth = maxDimension;
            newHeight = (int)((float)height / width * maxDimension);
        }
        else
        {
            newHeight = maxDimension;
            newWidth = (int)((float)width / height * maxDimension);
        }

        var result = new float[newWidth, newHeight];
        float xRatio = (float)(width - 1) / (newWidth - 1);
        float yRatio = (float)(height - 1) / (newHeight - 1);

        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                int srcX = (int)(x * xRatio);
                int srcY = (int)(y * yRatio);
                srcX = Math.Min(srcX, width - 1);
                srcY = Math.Min(srcY, height - 1);
                result[x, y] = image[srcX, srcY];
            }
        }

        return result;
    }
}