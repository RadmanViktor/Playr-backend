namespace Playr.Api.Models.Friends;

public sealed record FriendResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    DateTimeOffset FriendsSince);
