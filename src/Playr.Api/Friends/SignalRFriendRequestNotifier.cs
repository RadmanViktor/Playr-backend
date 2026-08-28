using Microsoft.AspNetCore.SignalR;
using Playr.Api.Hubs;
using Playr.Application.Friends;

namespace Playr.Api.Friends;

public sealed class SignalRFriendRequestNotifier(IHubContext<ChatHub> hubContext) : IFriendRequestNotifier
{
    public Task NotifyFriendRequestCreatedAsync(FriendRequestDto friendRequest, CancellationToken cancellationToken) =>
        hubContext.Clients
            .User(friendRequest.RecipientUserId.ToString())
            .SendAsync("FriendRequestReceived", friendRequest, cancellationToken);

    public Task NotifyFriendRequestUpdatedAsync(FriendRequestDto friendRequest, CancellationToken cancellationToken) =>
        hubContext.Clients
            .Users([friendRequest.SenderUserId.ToString(), friendRequest.RecipientUserId.ToString()])
            .SendAsync("FriendRequestUpdated", friendRequest, cancellationToken);
}
