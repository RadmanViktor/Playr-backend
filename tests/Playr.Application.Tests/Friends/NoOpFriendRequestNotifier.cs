using Playr.Application.Friends;

namespace Playr.Application.Tests.Friends;

public sealed class NoOpFriendRequestNotifier : IFriendRequestNotifier
{
    public Task NotifyFriendRequestCreatedAsync(FriendRequestDto friendRequest, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task NotifyFriendRequestUpdatedAsync(FriendRequestDto friendRequest, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
