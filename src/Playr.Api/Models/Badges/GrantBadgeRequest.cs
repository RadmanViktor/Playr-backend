namespace Playr.Api.Models.Badges;

/// <summary>
/// Level defaults to "Gold" if omitted - the special one-tier badges (like Creator)
/// don't have bronze/silver tiers.
/// </summary>
public sealed record GrantBadgeRequest(Guid UserId, string BadgeType, string? Level);
