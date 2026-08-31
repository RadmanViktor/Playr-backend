namespace Playr.Application.Lfg;

/// <summary>
/// Pushes LFG group events to connected clients in real time (e.g. via SignalR),
/// so lists and badges update without requiring a page reload.
/// </summary>
public interface ILfgGroupNotifier
{
    Task NotifyGroupUpdatedAsync(LfgGroupDto group, CancellationToken cancellationToken);

    Task NotifyApplicationReceivedAsync(Guid creatorUserId, LfgGroupApplicationDto application, CancellationToken cancellationToken);

    Task NotifyGroupInviteReceivedAsync(Guid inviteeUserId, LfgGroupInviteDto invite, CancellationToken cancellationToken);

    Task NotifyGroupFilledAsync(IReadOnlyList<Guid> memberUserIds, LfgGroupDto group, CancellationToken cancellationToken);
}
