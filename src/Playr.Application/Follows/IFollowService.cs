namespace Playr.Application.Follows;

public interface IFollowService
{
    Task<FollowDto> FollowAsync(Guid followerUserId, Guid followingUserId, CancellationToken cancellationToken);

    Task UnfollowAsync(Guid followerUserId, Guid followingUserId, CancellationToken cancellationToken);

    Task<bool> IsFollowingAsync(Guid followerUserId, Guid followingUserId, CancellationToken cancellationToken);

    Task<FollowCountsDto> GetCountsAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FollowDto>> GetFollowersAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FollowDto>> GetFollowingAsync(Guid userId, CancellationToken cancellationToken);
}
