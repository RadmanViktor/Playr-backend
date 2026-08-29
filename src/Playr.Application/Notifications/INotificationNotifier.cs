namespace Playr.Application.Notifications;

/// <summary>
/// Pushes notification-feed events to connected clients in real time (e.g. via SignalR),
/// so the bell badge/dropdown update without requiring a page reload.
/// </summary>
public interface INotificationNotifier
{
    Task NotifyNotificationCreatedAsync(NotificationDto notification, CancellationToken cancellationToken);
}
