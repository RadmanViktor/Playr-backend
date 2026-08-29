using Playr.Domain.Identity;

namespace Playr.Domain.Chat;

public sealed class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public Guid SenderUserId { get; set; }
    public ApplicationUser Sender { get; set; } = null!;
    public string Body { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public ChatMediaType? MediaType { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
}
