using Microsoft.AspNetCore.SignalR;
using Playr.Api.Hubs;
using Playr.Application.Profiles;
using Playr.Domain.Profiles;

namespace Playr.Api.Profiles;

public sealed class SignalRProfilePresenceNotifier(IHubContext<ChatHub> hubContext) : IProfilePresenceNotifier
{
    public Task NotifyStatusChangedAsync(Guid userId, ProfileStatus status, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync(
            "UserStatusChanged",
            new { userId, status = status.ToString() },
            cancellationToken);
}
