using Microsoft.AspNetCore.SignalR;
using Playr.Api.Hubs;
using Playr.Application.Follows;

namespace Playr.Api.Follows;

public sealed class SignalRFollowNotifier(IHubContext<ChatHub> hubContext) : IFollowNotifier
{
    public Task NotifyFollowCreatedAsync(FollowEventDto followEvent, CancellationToken cancellationToken) =>
        hubContext.Clients
            .User(followEvent.FollowingUserId.ToString())
            .SendAsync("FollowReceived", followEvent, cancellationToken);

    public Task NotifyFollowRemovedAsync(FollowEventDto followEvent, CancellationToken cancellationToken) =>
        hubContext.Clients
            .User(followEvent.FollowingUserId.ToString())
            .SendAsync("FollowRemoved", followEvent, cancellationToken);
}
