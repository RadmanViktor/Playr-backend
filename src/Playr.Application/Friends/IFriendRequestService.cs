namespace Playr.Application.Friends;

public interface IFriendRequestService
{
    Task<FriendRequestDto> SendAsync(Guid senderUserId, SendFriendRequestCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<FriendRequestDto>> GetIncomingAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FriendRequestDto>> GetSentAsync(Guid userId, CancellationToken cancellationToken);
    Task<FriendRequestDto> AcceptAsync(Guid userId, Guid friendRequestId, CancellationToken cancellationToken);
    Task<FriendRequestDto> DeclineAsync(Guid userId, Guid friendRequestId, CancellationToken cancellationToken);
    Task<FriendRequestDto> CancelAsync(Guid userId, Guid friendRequestId, CancellationToken cancellationToken);
}
