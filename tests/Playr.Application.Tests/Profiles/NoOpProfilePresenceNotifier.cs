using Playr.Application.Profiles;
using Playr.Domain.Profiles;

namespace Playr.Application.Tests.Profiles;

public sealed class NoOpProfilePresenceNotifier : IProfilePresenceNotifier
{
    public Task NotifyStatusChangedAsync(Guid userId, ProfileStatus status, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
