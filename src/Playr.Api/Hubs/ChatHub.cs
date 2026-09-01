using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Playr.Api.Extensions;
using Playr.Application.Profiles;

namespace Playr.Api.Hubs;

[Authorize]
public sealed class ChatHub(IProfileService profileService, IUserConnectionTracker connectionTracker) : Hub
{
    // A page refresh closes the old SignalR connection and opens a new one a moment later.
    // Give reconnects this long to show up before treating the user as truly offline, so a
    // refresh doesn't clobber a manually-selected status (e.g. Busy) back down to Offline.
    private static readonly TimeSpan DisconnectGracePeriod = TimeSpan.FromSeconds(8);

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.TryGetUserId(out var userId) == true)
        {
            var activeConnections = connectionTracker.AddConnection(userId, Context.ConnectionId);
            if (activeConnections == 1)
            {
                await profileService.SetOnlineIfOfflineAsync(userId, Context.ConnectionAborted);
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.User?.TryGetUserId(out var userId) == true)
        {
            var remainingConnections = connectionTracker.RemoveConnection(userId, Context.ConnectionId);
            if (remainingConnections == 0)
            {
                _ = MarkOfflineAfterGracePeriodAsync(userId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task MarkOfflineAfterGracePeriodAsync(Guid userId)
    {
        try
        {
            await Task.Delay(DisconnectGracePeriod);

            // If a new connection (e.g. from a page refresh) showed up during the grace
            // period, the user is still online - leave their persisted status untouched.
            if (!connectionTracker.HasConnections(userId))
            {
                await profileService.SetOfflineAsync(userId, CancellationToken.None);
            }
        }
        catch
        {
            // Best-effort presence tracking - never let this fail the hub connection lifecycle.
        }
    }
}
