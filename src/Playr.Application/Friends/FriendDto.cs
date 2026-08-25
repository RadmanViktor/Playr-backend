namespace Playr.Application.Friends;

public sealed record FriendDto(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    DateTimeOffset FriendsSince);
