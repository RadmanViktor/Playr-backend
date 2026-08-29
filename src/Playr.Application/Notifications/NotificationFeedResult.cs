namespace Playr.Application.Notifications;

public sealed record NotificationFeedResult(
    IReadOnlyList<NotificationDto> Items,
    bool HasMore,
    int UnreadCount);
