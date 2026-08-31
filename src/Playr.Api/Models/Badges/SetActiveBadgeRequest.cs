namespace Playr.Api.Models.Badges;

/// <summary>Pass a null/empty <see cref="BadgeType"/> to clear the active badge.</summary>
public sealed record SetActiveBadgeRequest(string? BadgeType);
