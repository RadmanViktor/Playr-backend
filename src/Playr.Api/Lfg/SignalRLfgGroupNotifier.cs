using Microsoft.AspNetCore.SignalR;
using Playr.Api.Hubs;
using Playr.Application.Lfg;

namespace Playr.Api.Lfg;

public sealed class SignalRLfgGroupNotifier(IHubContext<ChatHub> hubContext) : ILfgGroupNotifier
{
    public Task NotifyGroupUpdatedAsync(LfgGroupDto group, CancellationToken cancellationToken) =>
        // Open groups are visible to every authenticated user in the "Hitta spelare" list,
        // so the accepted-count/status change is broadcast to everyone rather than a
        // narrower set of recipients, keeping the counter live for anyone browsing.
        hubContext.Clients.All.SendAsync("LfgGroupUpdated", group, cancellationToken);

    public Task NotifyApplicationReceivedAsync(Guid creatorUserId, LfgGroupApplicationDto application, CancellationToken cancellationToken) =>
        hubContext.Clients
            .User(creatorUserId.ToString())
            .SendAsync("LfgApplicationReceived", application, cancellationToken);

    public Task NotifyGroupInviteReceivedAsync(Guid inviteeUserId, LfgGroupInviteDto invite, CancellationToken cancellationToken) =>
        hubContext.Clients
            .User(inviteeUserId.ToString())
            .SendAsync("LfgGroupInviteReceived", invite, cancellationToken);

    public Task NotifyGroupFilledAsync(IReadOnlyList<Guid> memberUserIds, LfgGroupDto group, CancellationToken cancellationToken) =>
        hubContext.Clients
            .Users(memberUserIds.Select(id => id.ToString()).ToList())
            .SendAsync("LfgGroupFilled", group, cancellationToken);
}
