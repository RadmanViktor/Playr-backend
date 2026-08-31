using Playr.Application.Lfg;

namespace Playr.Application.Tests.Lfg;

public sealed class NoOpLfgGroupNotifier : ILfgGroupNotifier
{
    public Task NotifyGroupUpdatedAsync(LfgGroupDto group, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task NotifyApplicationReceivedAsync(Guid creatorUserId, LfgGroupApplicationDto application, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task NotifyGroupInviteReceivedAsync(Guid inviteeUserId, LfgGroupInviteDto invite, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task NotifyGroupFilledAsync(IReadOnlyList<Guid> memberUserIds, LfgGroupDto group, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
