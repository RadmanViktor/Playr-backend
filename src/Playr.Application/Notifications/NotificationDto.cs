namespace Playr.Application.Notifications;

public sealed record NotificationActorDto(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);

public sealed record NotificationDto(
    Guid Id,
    string Type,
    bool IsRead,
    DateTimeOffset CreatedAt,
    NotificationActorDto Actor,
    Guid RecipientUserId,
    Guid? PostId,
    Guid? CommentId,
    string? BadgeType,
    string? BadgeLevel);
