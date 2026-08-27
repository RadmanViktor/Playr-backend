using Playr.Application.Invitations;

namespace Playr.Application.Tests.Invitations;

public sealed class NoOpInvitationNotifier : IInvitationNotifier
{
    public Task NotifyInvitationCreatedAsync(InvitationDto invitation, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task NotifyInvitationUpdatedAsync(InvitationDto invitation, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
