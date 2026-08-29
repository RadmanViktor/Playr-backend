using Microsoft.AspNetCore.SignalR;
using Playr.Api.Hubs;
using Playr.Application.Notifications;

namespace Playr.Api.Notifications;

public sealed class SignalRNotificationNotifier(IHubContext<ChatHub> hubContext) : INotificationNotifier
{
    public Task NotifyNotificationCreatedAsync(NotificationDto notification, CancellationToken cancellationToken) =>
        hubContext.Clients
            .User(notification.RecipientUserId.ToString())
            .SendAsync("NotificationReceived", notification, cancellationToken);
}
