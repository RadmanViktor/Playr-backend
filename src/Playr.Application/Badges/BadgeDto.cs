namespace Playr.Application.Badges;

public sealed record BadgeDto(
    string Type,
    string Level,
    DateTimeOffset UnlockedAt);

public sealed record UserBadgesDto(
    Guid UserId,
    IReadOnlyList<BadgeDto> Badges,
    string? ActiveBadgeType,
    string? ActiveBadgeLevel);
