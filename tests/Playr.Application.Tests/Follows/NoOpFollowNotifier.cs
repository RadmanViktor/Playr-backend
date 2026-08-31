using Playr.Application.Follows;

namespace Playr.Application.Tests.Follows;

public sealed class NoOpFollowNotifier : IFollowNotifier
{
    public Task NotifyFollowCreatedAsync(FollowEventDto followEvent, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task NotifyFollowRemovedAsync(FollowEventDto followEvent, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
