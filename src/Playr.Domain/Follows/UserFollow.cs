using Playr.Domain.Identity;

namespace Playr.Domain.Follows;

/// <summary>
/// Represents a one-way follow relationship. If User A follows User B, A wants to see more of
/// B's content and activity. B does not need to approve, and does not need to follow back.
/// </summary>
public sealed class UserFollow
{
    public Guid Id { get; set; }
    public Guid FollowerUserId { get; set; }
    public ApplicationUser Follower { get; set; } = null!;
    public Guid FollowingUserId { get; set; }
    public ApplicationUser Following { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
