namespace Playr.Application.Profiles;

using Playr.Domain.Profiles;

/// <summary>
/// Pushes profile online/offline status changes to connected clients in real time (e.g. via
/// SignalR), so status indicators (friends list, profile page, chat) update without requiring
/// a page reload.
/// </summary>
public interface IProfilePresenceNotifier
{
    Task NotifyStatusChangedAsync(Guid userId, ProfileStatus status, CancellationToken cancellationToken);
}
