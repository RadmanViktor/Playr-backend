namespace Playr.Api.Models.Notifications;

public sealed record NotificationActorResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);

public sealed record NotificationResponse(
    Guid Id,
    string Type,
    bool IsRead,
    DateTimeOffset CreatedAt,
    NotificationActorResponse Actor,
    Guid? PostId,
    Guid? CommentId);

public sealed record NotificationFeedResponse(
    IReadOnlyList<NotificationResponse> Items,
    bool HasMore,
    int UnreadCount);
