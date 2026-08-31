namespace Playr.Api.Models.Badges;

public sealed record BadgeResponse(
    string Type,
    string Level,
    DateTimeOffset UnlockedAt);

public sealed record UserBadgesResponse(
    Guid UserId,
    IReadOnlyList<BadgeResponse> Badges,
    string? ActiveBadgeType,
    string? ActiveBadgeLevel);
