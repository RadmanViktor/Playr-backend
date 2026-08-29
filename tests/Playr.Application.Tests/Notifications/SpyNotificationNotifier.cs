using Playr.Application.Notifications;

namespace Playr.Application.Tests.Notifications;

public sealed class SpyNotificationNotifier : INotificationNotifier
{
    public List<NotificationDto> Notified { get; } = [];

    public Task NotifyNotificationCreatedAsync(NotificationDto notification, CancellationToken cancellationToken)
    {
        Notified.Add(notification);
        return Task.CompletedTask;
    }
}
