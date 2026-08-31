namespace Playr.Application.Follows;

public sealed record FollowEventDto(
    Guid FollowerUserId,
    string FollowerUsername,
    string FollowerDisplayName,
    string? FollowerAvatarUrl,
    Guid FollowingUserId,
    string FollowingUsername,
    string FollowingDisplayName,
    string? FollowingAvatarUrl,
    DateTimeOffset CreatedAt);
