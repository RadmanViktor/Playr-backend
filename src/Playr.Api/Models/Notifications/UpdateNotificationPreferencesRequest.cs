namespace Playr.Api.Models.Notifications;

public sealed record UpdateNotificationPreferencesRequest(bool ChatSoundEnabled, bool ChatBrowserNotificationsEnabled);
