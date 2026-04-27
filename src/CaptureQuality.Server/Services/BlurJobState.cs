namespace CaptureQuality.Server.Services;

public enum BlurJobState
{
    Queued,
    Running,
    Completed,
    Failed,
    Canceled,
    Expired
}
