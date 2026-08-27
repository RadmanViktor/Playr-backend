namespace Playr.Api.Models.Friends;

public sealed record FriendRequestResponse(
    Guid Id,
    Guid SenderUserId,
    string SenderUsername,
    string SenderDisplayName,
    string? SenderAvatarUrl,
    Guid RecipientUserId,
    string RecipientUsername,
    string RecipientDisplayName,
    string? RecipientAvatarUrl,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt);
