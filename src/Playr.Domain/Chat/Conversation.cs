using Playr.Domain.Identity;

namespace Playr.Domain.Chat;

public sealed class Conversation
{
    public Guid Id { get; set; }
    public Guid DirectUserAId { get; set; }
    public ApplicationUser DirectUserA { get; set; } = null!;
    public Guid DirectUserBId { get; set; }
    public ApplicationUser DirectUserB { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<ConversationParticipant> Participants { get; set; } = [];
    public ICollection<ChatMessage> Messages { get; set; } = [];
}
