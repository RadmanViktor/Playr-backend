using Playr.Domain.Identity;

namespace Playr.Domain.Invitations;

public sealed class Invitation
{
    public Guid Id { get; set; }
    public Guid SenderUserId { get; set; }
    public ApplicationUser Sender { get; set; } = null!;
    public Guid RecipientUserId { get; set; }
    public ApplicationUser Recipient { get; set; } = null!;
    public string Message { get; set; } = string.Empty;
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RespondedAt { get; set; }
}
