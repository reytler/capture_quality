using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaptureQuality.Models;

namespace CaptureQuality.Services;

public class ImageProcessingQueueService
{
    private const int MaxUploadBytes = 10 * 1024 * 1024;
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, ProcessingQueueItem> _queue = new();
    private readonly SemaphoreSlim _processingSemaphore = new(1);
    private bool _isProcessing;

    public ImageProcessingQueueService(HttpClient http)
    {
        _http = http;
    }

    public event Action<string, int>? OnProgressUpdate;
    public event Action<string>? OnItemCompleted;
    public event Action? OnQueueChanged;

    public IReadOnlyList<ProcessingQueueItem> Items => _queue.Values.OrderByDescending(x => x.Timestamp).ToList();

    public async Task<ProcessingQueueItem> AddAsync(string imageData)
    {
        Console.WriteLine($"[ImageProcessingQueueService:AddAsync] ENTRY - imageData length: {imageData?.Length ?? 0}");
        
        var item = new ProcessingQueueItem
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            ImageData = imageData,
            Thumbnail = GenerateThumbnail(imageData),
            Status = QueueItemStatus.Pending
        };
        
        Console.WriteLine($"[ImageProcessingQueueService:AddAsync] Created item ID: {item.Id}");
        
        _queue[item.Id] = item;
        Console.WriteLine($"[ImageProcessingQueueService:AddAsync] Added to queue, triggering processing...");
        
        OnQueueChanged?.Invoke();
        _ = ProcessQueueAsync();
        return item;
    }

    private string GenerateThumbnail(string imageData)
    {
        return imageData;
    }

    private async Task ProcessQueueAsync()
    {
        Console.WriteLine($"[ImageProcessingQueueService:ProcessQueueAsync] ENTRY - _isProcessing: {_isProcessing}");
        
        if (_isProcessing) return;
        _isProcessing = true;

        try
        {
            while (true)
            {
                var pending = _queue.Values.FirstOrDefault(x => x.Status == QueueItemStatus.Pending);
                if (pending == null)
                {
                    Console.WriteLine($"[ImageProcessingQueueService:ProcessQueueAsync] No pending items, exiting loop");
                    break;
                }

                Console.WriteLine($"[ImageProcessingQueueService:ProcessQueueAsync] Processing item ID: {pending.Id}");
                pending.Status = QueueItemStatus.Processing;
                OnQueueChanged?.Invoke();

                try
                {
                    pending.Progress = 10;
                    OnProgressUpdate?.Invoke(pending.Id, pending.Progress);

                    var bytes = DecodeDataUrlToBytes(pending.ImageData);
                    if (bytes.Length > MaxUploadBytes)
                    {
                        throw new InvalidOperationException("Image exceeds 10MB limit");
                    }

                    var contentType = TryGetDataUrlContentType(pending.ImageData) ?? "application/octet-stream";
                    var fileName = contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase)
                        ? "capture.png"
                        : contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
                            ? "capture.jpg"
                            : "capture.bin";

                    using var fileContent = new ByteArrayContent(bytes);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                    using var form = new MultipartFormDataContent();
                    form.Add(fileContent, "file", fileName);

                    using var response = await _http.PostAsync("api/blur-detection", form);
                    if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
                    {
                        throw new InvalidOperationException("Image exceeds 10MB limit");
                    }

                    response.EnsureSuccessStatusCode();

                    var metrics = await response.Content.ReadFromJsonAsync<BlurDetectionMetricsDto>();
                    if (metrics is null)
                    {
                        throw new InvalidOperationException("Empty blur-detection response");
                    }

                    pending.Result = new BlurDetectionResult
                    {
                        IsAccepted = metrics.IsAccepted,
                        BlurRatio = metrics.BlurRatio,
                        TotalPatches = metrics.TotalPatches,
                        BlurredPatches = metrics.BlurredPatches,
                        Status = metrics.Status,
                        PatchSize = metrics.PatchSize,
                        ImageWidth = metrics.ImageWidth,
                        ImageHeight = metrics.ImageHeight,
                        BlurMap = null
                    };

                    pending.Progress = 100;
                    OnProgressUpdate?.Invoke(pending.Id, pending.Progress);
                    pending.Status = QueueItemStatus.Completed;
                    OnItemCompleted?.Invoke(pending.Id);
                    Console.WriteLine($"[ImageProcessingQueueService:ProcessQueueAsync] Item {pending.Id} completed - BlurRatio: {pending.Result.BlurRatio:F2}");
                }
                catch (Exception ex)
                {
                    pending.Status = QueueItemStatus.Failed;
                    pending.ErrorMessage = ex.Message;
                    Console.WriteLine($"[ImageProcessingQueueService:ProcessQueueAsync] Item {pending.Id} FAILED: {ex.Message}");
                }

                OnQueueChanged?.Invoke();
            }
        }
        finally
        {
            _isProcessing = false;
            Console.WriteLine($"[ImageProcessingQueueService:ProcessQueueAsync] EXIT");
        }
    }

    public void Clear()
    {
        _queue.Clear();
        OnQueueChanged?.Invoke();
    }

    private static string? TryGetDataUrlContentType(string dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;
        if (!dataUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return null;

        int semi = dataUrl.IndexOf(';');
        if (semi <= "data:".Length) return null;

        return dataUrl["data:".Length..semi];
    }

    private static byte[] DecodeDataUrlToBytes(string dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            throw new InvalidOperationException("Missing image data");
        }

        int comma = dataUrl.IndexOf(',');
        if (comma < 0 || comma == dataUrl.Length - 1)
        {
            throw new InvalidOperationException("Invalid image data URL");
        }

        var base64 = dataUrl[(comma + 1)..];
        return Convert.FromBase64String(base64);
    }
}
