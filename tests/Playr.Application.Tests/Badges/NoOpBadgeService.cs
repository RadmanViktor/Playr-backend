using Playr.Application.Badges;
using Playr.Domain.Badges;

namespace Playr.Application.Tests.Badges;

public sealed class NoOpBadgeService : IBadgeService
{
    public Task<UserBadgesDto> GetBadgesAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(new UserBadgesDto(userId, [], null, null));

    public Task SetActiveBadgeAsync(Guid userId, BadgeType? badgeType, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task CheckAndUnlockBadgesAsync(Guid userId, BadgeType type, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task CheckFirstHundredUsersAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task GrantBadgeAsync(Guid userId, BadgeType type, BadgeLevel level, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task CheckVeteranStatusAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
