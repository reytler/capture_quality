using Microsoft.AspNetCore.SignalR;

namespace CaptureQuality.Server.Hubs;

public sealed class BlurJobHub : Hub
{
    public Task Subscribe(string jobId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(jobId));
    }

    public Task Unsubscribe(string jobId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(jobId));
    }

    public static string GetGroupName(string jobId) => $"job:{jobId}";
}
