using Microsoft.AspNetCore.SignalR;
using Playr.Api.Hubs;
using Playr.Application.Invitations;

namespace Playr.Api.Invitations;

public sealed class SignalRInvitationNotifier(IHubContext<ChatHub> hubContext) : IInvitationNotifier
{
    public Task NotifyInvitationCreatedAsync(InvitationDto invitation, CancellationToken cancellationToken) =>
        hubContext.Clients
            .User(invitation.RecipientUserId.ToString())
            .SendAsync("InvitationReceived", invitation, cancellationToken);

    public Task NotifyInvitationUpdatedAsync(InvitationDto invitation, CancellationToken cancellationToken) =>
        hubContext.Clients
            .Users([invitation.SenderUserId.ToString(), invitation.RecipientUserId.ToString()])
            .SendAsync("InvitationUpdated", invitation, cancellationToken);
}
