using Playr.Domain.Identity;

namespace Playr.Domain.Badges;

public sealed class UserBadge
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public BadgeType Type { get; set; }
    public BadgeLevel Level { get; set; } = BadgeLevel.None;
    public DateTimeOffset UnlockedAt { get; set; } = DateTimeOffset.UtcNow;
}
