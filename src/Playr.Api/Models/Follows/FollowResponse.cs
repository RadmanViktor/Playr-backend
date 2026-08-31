namespace Playr.Api.Models.Follows;

public sealed record FollowResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    DateTimeOffset FollowingSince);
