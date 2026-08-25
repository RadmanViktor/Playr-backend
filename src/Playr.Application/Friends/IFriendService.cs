namespace Playr.Application.Friends;

public interface IFriendService
{
    Task<IReadOnlyList<FriendDto>> GetFriendsAsync(Guid userId, CancellationToken cancellationToken);
}
