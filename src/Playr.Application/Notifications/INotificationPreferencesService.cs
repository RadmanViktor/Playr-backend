namespace Playr.Application.Notifications;

public interface INotificationPreferencesService
{
    Task<NotificationPreferencesDto> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<NotificationPreferencesDto> UpdateAsync(
        Guid userId,
        UpdateNotificationPreferencesCommand command,
        CancellationToken cancellationToken);
}
