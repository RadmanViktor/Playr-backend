using Playr.Domain.Identity;

namespace Playr.Domain.Notifications;

public sealed class Notification
{
    public Guid Id { get; set; }
    public Guid RecipientUserId { get; set; }
    public ApplicationUser Recipient { get; set; } = null!;
    public Guid ActorUserId { get; set; }
    public ApplicationUser Actor { get; set; } = null!;
    public NotificationType Type { get; set; }
    public Guid PostId { get; set; }
    public Guid? CommentId { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
