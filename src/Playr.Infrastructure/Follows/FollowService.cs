using Microsoft.EntityFrameworkCore;
using Playr.Application.Follows;
using Playr.Application.Notifications;
using Playr.Domain.Follows;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Follows;

public sealed class FollowService(PlayrDbContext dbContext, IFollowNotifier followNotifier, INotificationFeedService notificationFeedService) : IFollowService
{
    public async Task<FollowDto> FollowAsync(Guid followerUserId, Guid followingUserId, CancellationToken cancellationToken)
    {
        if (followerUserId == followingUserId)
        {
            throw new InvalidOperationException("You cannot follow yourself.");
        }

        var followingProfile = await dbContext.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == followingUserId, cancellationToken)
            ?? throw new InvalidOperationException("Player was not found.");

        var existing = await dbContext.UserFollows
            .FirstOrDefaultAsync(f => f.FollowerUserId == followerUserId && f.FollowingUserId == followingUserId, cancellationToken);

        if (existing is not null)
        {
            return new FollowDto(followingProfile.UserId, followingProfile.Username, followingProfile.DisplayName, followingProfile.AvatarUrl, existing.CreatedAt);
        }

        var follow = new UserFollow
        {
            Id = Guid.NewGuid(),
            FollowerUserId = followerUserId,
            FollowingUserId = followingUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.UserFollows.Add(follow);
        await dbContext.SaveChangesAsync(cancellationToken);

        var followerProfile = await dbContext.UserProfiles.AsNoTracking()
            .FirstAsync(p => p.UserId == followerUserId, cancellationToken);

        await followNotifier.NotifyFollowCreatedAsync(
            new FollowEventDto(
                followerProfile.UserId, followerProfile.Username, followerProfile.DisplayName, followerProfile.AvatarUrl,
                followingProfile.UserId, followingProfile.Username, followingProfile.DisplayName, followingProfile.AvatarUrl,
                follow.CreatedAt),
            cancellationToken);

        await notificationFeedService.CreateFollowNotificationAsync(followerUserId, followingUserId, cancellationToken);

        return new FollowDto(followingProfile.UserId, followingProfile.Username, followingProfile.DisplayName, followingProfile.AvatarUrl, follow.CreatedAt);
    }

    public async Task UnfollowAsync(Guid followerUserId, Guid followingUserId, CancellationToken cancellationToken)
    {
        var follow = await dbContext.UserFollows
            .FirstOrDefaultAsync(f => f.FollowerUserId == followerUserId && f.FollowingUserId == followingUserId, cancellationToken);

        if (follow is null)
        {
            return;
        }

        dbContext.UserFollows.Remove(follow);
        await dbContext.SaveChangesAsync(cancellationToken);

        var profiles = await dbContext.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == followerUserId || p.UserId == followingUserId)
            .ToListAsync(cancellationToken);
        var followerProfile = profiles.First(p => p.UserId == followerUserId);
        var followingProfile = profiles.First(p => p.UserId == followingUserId);

        await followNotifier.NotifyFollowRemovedAsync(
            new FollowEventDto(
                followerProfile.UserId, followerProfile.Username, followerProfile.DisplayName, followerProfile.AvatarUrl,
                followingProfile.UserId, followingProfile.Username, followingProfile.DisplayName, followingProfile.AvatarUrl,
                follow.CreatedAt),
            cancellationToken);
    }

    public Task<bool> IsFollowingAsync(Guid followerUserId, Guid followingUserId, CancellationToken cancellationToken) =>
        dbContext.UserFollows.AsNoTracking()
            .AnyAsync(f => f.FollowerUserId == followerUserId && f.FollowingUserId == followingUserId, cancellationToken);

    public async Task<FollowCountsDto> GetCountsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var followersCount = await dbContext.UserFollows.AsNoTracking()
            .CountAsync(f => f.FollowingUserId == userId, cancellationToken);
        var followingCount = await dbContext.UserFollows.AsNoTracking()
            .CountAsync(f => f.FollowerUserId == userId, cancellationToken);
        return new FollowCountsDto(followersCount, followingCount);
    }

    public async Task<IReadOnlyList<FollowDto>> GetFollowersAsync(Guid userId, CancellationToken cancellationToken)
    {
        var follows = await dbContext.UserFollows.AsNoTracking()
            .Where(f => f.FollowingUserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

        return await HydrateAsync(follows, f => f.FollowerUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<FollowDto>> GetFollowingAsync(Guid userId, CancellationToken cancellationToken)
    {
        var follows = await dbContext.UserFollows.AsNoTracking()
            .Where(f => f.FollowerUserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

        return await HydrateAsync(follows, f => f.FollowingUserId, cancellationToken);
    }

    private async Task<IReadOnlyList<FollowDto>> HydrateAsync(
        List<UserFollow> follows,
        Func<UserFollow, Guid> selectUserId,
        CancellationToken cancellationToken)
    {
        if (follows.Count == 0)
        {
            return [];
        }

        var userIds = follows.Select(selectUserId).ToList();
        var profiles = await dbContext.UserProfiles.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);
        var profileMap = profiles.ToDictionary(p => p.UserId);

        return follows
            .Where(f => profileMap.ContainsKey(selectUserId(f)))
            .Select(f =>
            {
                var profile = profileMap[selectUserId(f)];
                return new FollowDto(profile.UserId, profile.Username, profile.DisplayName, profile.AvatarUrl, f.CreatedAt);
            })
            .ToList();
    }
}
