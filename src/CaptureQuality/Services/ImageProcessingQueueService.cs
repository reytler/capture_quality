using System.Collections.Concurrent;
using CaptureQuality.Models;

namespace CaptureQuality.Services;

public class ImageProcessingQueueService
{
    private readonly BlurDetectorService _blurDetector;
    private readonly ConcurrentDictionary<string, ProcessingQueueItem> _queue = new();
    private readonly SemaphoreSlim _processingSemaphore = new(1);
    private bool _isProcessing;

    public ImageProcessingQueueService(BlurDetectorService blurDetector)
    {
        _blurDetector = blurDetector;
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
                    Console.WriteLine($"[ImageProcessingQueueService:ProcessQueueAsync] Calling BlurDetector.DetectBlurAsync...");
                    var result = await _blurDetector.DetectBlurAsync(
                        new MemoryStream(Convert.FromBase64String(pending.ImageData.Split(',')[1])),
                        progress =>
                        {
                            pending.Progress = progress;
                            OnProgressUpdate?.Invoke(pending.Id, progress);
                        });
                    pending.Result = result;
                    pending.Status = QueueItemStatus.Completed;
                    OnItemCompleted?.Invoke(pending.Id);
                    Console.WriteLine($"[ImageProcessingQueueService:ProcessQueueAsync] Item {pending.Id} completed - BlurRatio: {result.BlurRatio:F2}");
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
}