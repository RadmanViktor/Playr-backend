using Playr.Domain.Badges;

namespace Playr.Application.Badges;

public interface IBadgeService
{
    Task<UserBadgesDto> GetBadgesAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the caller's active badge (shown as the avatar ring). Pass <c>null</c> for
    /// <paramref name="badgeType"/> to clear the active badge. Throws
    /// <see cref="InvalidOperationException"/> if the requested badge has not been unlocked
    /// (i.e. its current level is <see cref="BadgeLevel.None"/>).
    /// </summary>
    Task SetActiveBadgeAsync(Guid userId, BadgeType? badgeType, CancellationToken cancellationToken);

    /// <summary>
    /// Recomputes the relevant stat for <paramref name="userId"/> and <paramref name="type"/>
    /// and, if a new tier was reached, updates the <see cref="UserBadge"/> row, auto-activates
    /// the badge if the user has no active badge yet, and sends a
    /// <see cref="Playr.Domain.Notifications.NotificationType.BadgeUnlocked"/> notification.
    /// Safe to call after every relevant user action (post created, comment created, game
    /// rated, invitation accepted). Never throws for "no new tier reached".
    /// </summary>
    Task CheckAndUnlockBadgesAsync(Guid userId, BadgeType type, CancellationToken cancellationToken);

    /// <summary>
    /// One-time check run right after a new user's profile is persisted: if the user's
    /// signup rank (by registration order) is within the first
    /// <see cref="BadgeThresholds.FirstHundredUsersCount"/> registered users, unlocks
    /// <see cref="BadgeType.FirstHundredUsers"/> for them.
    /// </summary>
    Task CheckFirstHundredUsersAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Manually grants <paramref name="type"/> at <paramref name="level"/> to
    /// <paramref name="userId"/>, bypassing stat thresholds entirely. Used for
    /// non-stat-based badges such as <see cref="BadgeType.Creator"/>. Auto-activates
    /// the badge if the user has no active badge yet, and sends a
    /// <see cref="Playr.Domain.Notifications.NotificationType.BadgeUnlocked"/> notification,
    /// same as <see cref="CheckAndUnlockBadgesAsync"/>. No-ops if the user already has
    /// this badge at this level or higher.
    /// </summary>
    Task GrantBadgeAsync(Guid userId, BadgeType type, BadgeLevel level, CancellationToken cancellationToken);
}
