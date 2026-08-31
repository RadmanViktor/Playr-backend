namespace Playr.Application.Friends;

public interface IFriendService
{
    Task<IReadOnlyList<FriendDto>> GetFriendsAsync(Guid userId, CancellationToken cancellationToken);

    Task<int> GetFriendsCountAsync(Guid userId, CancellationToken cancellationToken);
}
