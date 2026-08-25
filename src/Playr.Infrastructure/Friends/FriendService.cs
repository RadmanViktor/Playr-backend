using Microsoft.EntityFrameworkCore;
using Playr.Application.Friends;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Friends;

public sealed class FriendService(PlayrDbContext dbContext) : IFriendService
{
    public async Task<IReadOnlyList<FriendDto>> GetFriendsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var friendships = await dbContext.Friendships.AsNoTracking()
            .Where(f => f.UserAId == userId || f.UserBId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

        if (friendships.Count == 0)
        {
            return [];
        }

        var friendIds = friendships.Select(f => f.UserAId == userId ? f.UserBId : f.UserAId).ToList();
        var profiles = await dbContext.UserProfiles.AsNoTracking()
            .Where(p => friendIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);
        var profileMap = profiles.ToDictionary(p => p.UserId);

        return friendships.Select(friendship =>
        {
            var friendId = friendship.UserAId == userId ? friendship.UserBId : friendship.UserAId;
            var profile = profileMap[friendId];
            return new FriendDto(profile.UserId, profile.Username, profile.DisplayName, profile.AvatarUrl, friendship.CreatedAt);
        }).ToList();
    }
}
