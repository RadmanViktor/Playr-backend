namespace Playr.Application.Invitations;

/// <summary>
/// Pushes invitation events to connected clients in real time (e.g. via SignalR),
/// so badges/lists update without requiring a page reload.
/// </summary>
public interface IInvitationNotifier
{
    Task NotifyInvitationCreatedAsync(InvitationDto invitation, CancellationToken cancellationToken);

    Task NotifyInvitationUpdatedAsync(InvitationDto invitation, CancellationToken cancellationToken);
}
