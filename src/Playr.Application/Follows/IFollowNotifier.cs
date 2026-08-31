namespace Playr.Application.Follows;

/// <summary>
/// Pushes follow events to connected clients in real time (e.g. via SignalR), so follower
/// counts and follow buttons update without requiring a page reload.
/// </summary>
public interface IFollowNotifier
{
    Task NotifyFollowCreatedAsync(FollowEventDto followEvent, CancellationToken cancellationToken);

    Task NotifyFollowRemovedAsync(FollowEventDto followEvent, CancellationToken cancellationToken);
}
