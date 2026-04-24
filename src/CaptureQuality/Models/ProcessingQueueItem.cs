namespace CaptureQuality.Models;

public enum QueueItemStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

public class ProcessingQueueItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string ImageData { get; set; } = "";
    public string Thumbnail { get; set; } = "";
    public QueueItemStatus Status { get; set; } = QueueItemStatus.Pending;
    public int Progress { get; set; } = 0;
    public BlurDetectionResult? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
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