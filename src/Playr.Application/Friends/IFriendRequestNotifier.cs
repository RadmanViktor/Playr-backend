namespace Playr.Application.Friends;

/// <summary>
/// Pushes friend request events to connected clients in real time (e.g. via SignalR),
/// so badges/lists update without requiring a page reload.
/// </summary>
public interface IFriendRequestNotifier
{
    Task NotifyFriendRequestCreatedAsync(FriendRequestDto friendRequest, CancellationToken cancellationToken);

    Task NotifyFriendRequestUpdatedAsync(FriendRequestDto friendRequest, CancellationToken cancellationToken);
}
