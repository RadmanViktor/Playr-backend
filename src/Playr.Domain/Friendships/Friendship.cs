using Playr.Domain.Identity;

namespace Playr.Domain.Friendships;

/// <summary>
/// Represents a mutual friendship between two users, created when one user accepts
/// the other's invitation. UserAId is always the smaller Guid (by comparison) so that
/// each pair of users has exactly one row regardless of who sent the original invitation.
/// </summary>
public sealed class Friendship
{
    public Guid Id { get; set; }
    public Guid UserAId { get; set; }
    public ApplicationUser UserA { get; set; } = null!;
    public Guid UserBId { get; set; }
    public ApplicationUser UserB { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
