using Microsoft.EntityFrameworkCore;
using Playr.Application.Badges;
using Playr.Application.Notifications;
using Playr.Domain.Badges;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Badges;

public sealed class BadgeService(PlayrDbContext dbContext, INotificationFeedService notificationFeedService) : IBadgeService
{
    public async Task<UserBadgesDto> GetBadgesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var badges = await dbContext.UserBadges
            .AsNoTracking()
            .Where(b => b.UserId == userId && b.Level != BadgeLevel.None)
            .OrderBy(b => b.Type)
            .Select(b => new BadgeDto(b.Type.ToString(), b.Level.ToString(), b.UnlockedAt))
            .ToListAsync(cancellationToken);

        var profile = await dbContext.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("User profile was not found.");

        return new UserBadgesDto(
            userId,
            badges,
            profile.ActiveBadgeType?.ToString(),
            profile.ActiveBadgeLevel?.ToString());
    }

    public async Task SetActiveBadgeAsync(Guid userId, BadgeType? badgeType, CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("User profile was not found.");

        if (badgeType is null)
        {
            profile.ActiveBadgeType = null;
            profile.ActiveBadgeLevel = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var badge = await dbContext.UserBadges
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Type == badgeType.Value, cancellationToken);

        if (badge is null || badge.Level == BadgeLevel.None)
        {
            throw new InvalidOperationException("You have not unlocked this badge yet.");
        }

        profile.ActiveBadgeType = badge.Type;
        profile.ActiveBadgeLevel = badge.Level;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CheckAndUnlockBadgesAsync(Guid userId, BadgeType type, CancellationToken cancellationToken)
    {
        if (type == BadgeType.FirstHundredUsers)
        {
            // Not stat-based; only unlocked via CheckFirstHundredUsersAsync at registration time.
            return;
        }

        var statValue = await ComputeStatAsync(userId, type, cancellationToken);
        var newLevel = BadgeThresholds.GetLevelForStat(type, statValue);
        if (newLevel == BadgeLevel.None)
        {
            return;
        }

        await UnlockIfHigherAsync(userId, type, newLevel, cancellationToken);
    }

    public async Task CheckFirstHundredUsersAsync(Guid userId, CancellationToken cancellationToken)
    {
        var registeredCount = await dbContext.UserProfiles
            .AsNoTracking()
            .CountAsync(p => p.UserId != userId, cancellationToken);

        if (registeredCount >= BadgeThresholds.FirstHundredUsersCount)
        {
            return;
        }

        await UnlockIfHigherAsync(userId, BadgeType.FirstHundredUsers, BadgeLevel.Gold, cancellationToken);
    }

    public async Task GrantBadgeAsync(Guid userId, BadgeType type, BadgeLevel level, CancellationToken cancellationToken)
    {
        var userExists = await dbContext.UserProfiles.AsNoTracking().AnyAsync(p => p.UserId == userId, cancellationToken);
        if (!userExists)
        {
            throw new InvalidOperationException("User was not found.");
        }

        await UnlockIfHigherAsync(userId, type, level, cancellationToken);
    }

    private async Task<int> ComputeStatAsync(Guid userId, BadgeType type, CancellationToken cancellationToken) => type switch
    {
        BadgeType.Poster => await dbContext.Posts.CountAsync(p => p.AuthorId == userId, cancellationToken),
        BadgeType.GameCritic => await dbContext.UserGameLibraryEntries.CountAsync(
            e => e.UserId == userId && e.Rating != null, cancellationToken),
        BadgeType.Commentator => await dbContext.PostComments
            .Where(c => c.AuthorId == userId)
            .Join(dbContext.Posts, c => c.PostId, p => p.Id, (c, p) => new { c, p })
            .CountAsync(x => x.p.AuthorId != userId, cancellationToken),
        BadgeType.Inviter => await dbContext.Invitations.CountAsync(
            i => i.SenderUserId == userId && i.Status == Domain.Invitations.InvitationStatus.Accepted, cancellationToken),
        _ => 0,
    };

    private async Task UnlockIfHigherAsync(Guid userId, BadgeType type, BadgeLevel newLevel, CancellationToken cancellationToken)
    {
        var badge = await dbContext.UserBadges
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Type == type, cancellationToken);

        if (badge is null)
        {
            badge = new UserBadge
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = type,
                Level = BadgeLevel.None,
            };
            dbContext.UserBadges.Add(badge);
        }

        if (newLevel <= badge.Level)
        {
            return;
        }

        badge.Level = newLevel;
        badge.UnlockedAt = DateTimeOffset.UtcNow;

        var profile = await dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is not null && profile.ActiveBadgeType is null)
        {
            profile.ActiveBadgeType = type;
            profile.ActiveBadgeLevel = newLevel;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await notificationFeedService.CreateBadgeUnlockedNotificationAsync(userId, type, newLevel, cancellationToken);
    }
}
