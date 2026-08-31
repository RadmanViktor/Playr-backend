namespace Playr.Application.Follows;

public sealed record FollowDto(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    DateTimeOffset FollowingSince);
