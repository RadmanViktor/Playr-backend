using Playr.Domain.Identity;

namespace Playr.Domain.Friendships;

/// <summary>
/// A standalone friend request, sent directly from a user profile, independent of the
/// "Find Players" invitation flow. Accepting a request creates a <see cref="Friendship"/>.
/// </summary>
public sealed class FriendRequest
{
    public Guid Id { get; set; }
    public Guid SenderUserId { get; set; }
    public ApplicationUser Sender { get; set; } = null!;
    public Guid RecipientUserId { get; set; }
    public ApplicationUser Recipient { get; set; } = null!;
    public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RespondedAt { get; set; }
}
