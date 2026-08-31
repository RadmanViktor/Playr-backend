using Playr.Domain.Identity;

namespace Playr.Domain.Chat;

public sealed class Conversation
{
    public Guid Id { get; set; }
    public ConversationType Type { get; set; } = ConversationType.Direct;
    public string? Title { get; set; }
    public Guid? LfgGroupId { get; set; }
    public Guid? DirectUserAId { get; set; }
    public ApplicationUser? DirectUserA { get; set; }
    public Guid? DirectUserBId { get; set; }
    public ApplicationUser? DirectUserB { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<ConversationParticipant> Participants { get; set; } = [];
    public ICollection<ChatMessage> Messages { get; set; } = [];
}
