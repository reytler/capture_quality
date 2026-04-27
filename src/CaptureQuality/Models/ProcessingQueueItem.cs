namespace CaptureQuality.Models;

public enum QueueItemStatus
{
    Pending,
    Processing,
    Canceled,
    Completed,
    Failed
}

public class ProcessingQueueItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string? ServerJobId { get; set; }
    public string Thumbnail { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public QueueItemStatus Status { get; set; } = QueueItemStatus.Pending;
    public int Progress { get; set; } = 0;
    public BlurDetectionResult? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Stage { get; set; }
    public string? Message { get; set; }
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

public class CapturedImagePayload
{
    public string ThumbnailUrl { get; set; } = "";
    public string FileName { get; set; } = "capture.bin";
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[]? Bytes { get; set; }
    public Microsoft.AspNetCore.Components.Forms.IBrowserFile? BrowserFile { get; set; }
}

public class CreateBlurJobResponse
{
    public string JobId { get; set; } = "";
    public string Status { get; set; } = "";
    public int Progress { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BlurJobUpdateDto
{
    public string JobId { get; set; } = "";
    public string Status { get; set; } = "";
    public int Progress { get; set; }
    public string Stage { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
    public BlurDetectionMetricsDto? Result { get; set; }
}
