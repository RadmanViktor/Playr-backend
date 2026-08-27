namespace Playr.Application.Notifications;

public sealed record UpdateNotificationPreferencesCommand(bool ChatSoundEnabled, bool ChatBrowserNotificationsEnabled);
