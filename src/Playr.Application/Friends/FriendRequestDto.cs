using Playr.Domain.Friendships;

namespace Playr.Application.Friends;

public sealed record FriendRequestDto(
    Guid Id,
    Guid SenderUserId,
    string SenderUsername,
    string SenderDisplayName,
    string? SenderAvatarUrl,
    Guid RecipientUserId,
    string RecipientUsername,
    string RecipientDisplayName,
    string? RecipientAvatarUrl,
    FriendRequestStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt);
