using Microsoft.AspNetCore.SignalR;

namespace SharePointPermissionSync.Web.Hubs;

/// <summary>
/// SignalR hub for real-time job progress updates
/// </summary>
public class JobProgressHub : Hub
{
    private readonly ILogger<JobProgressHub> _logger;

    public JobProgressHub(ILogger<JobProgressHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to job progress updates
    /// </summary>
    public async Task SubscribeToJob(string jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Job-{jobId}");
        _logger.LogInformation(
            "Client {ConnectionId} subscribed to job {JobId}",
            Context.ConnectionId,
            jobId);
    }

    /// <summary>
    /// Unsubscribe from job progress updates
    /// </summary>
    public async Task UnsubscribeFromJob(string jobId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Job-{jobId}");
        _logger.LogInformation(
            "Client {ConnectionId} unsubscribed from job {JobId}",
            Context.ConnectionId,
            jobId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
