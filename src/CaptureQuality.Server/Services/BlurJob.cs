using CaptureQuality.Models;

namespace CaptureQuality.Server.Services;

public sealed class BlurJob
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string FileName { get; init; } = "capture.bin";
    public string ContentType { get; init; } = "application/octet-stream";
    public byte[] ImageBytes { get; init; } = [];
    public BlurJobState State { get; set; } = BlurJobState.Queued;
    public int Progress { get; set; }
    public string Stage { get; set; } = "queued";
    public string Message { get; set; } = "Job queued";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
    public BlurDetectionMetricsDto? Result { get; set; }
    public CancellationTokenSource Cancellation { get; } = new();
}
