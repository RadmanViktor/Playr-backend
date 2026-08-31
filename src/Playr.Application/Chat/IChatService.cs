namespace Playr.Application.Chat;

public interface IChatService
{
    Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(Guid userId, CancellationToken cancellationToken);
    Task<ConversationDto> GetOrCreateDirectConversationAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken);
    Task<ConversationDto> GetOrCreateGroupConversationAsync(IReadOnlyList<Guid> memberUserIds, string? title, Guid? lfgGroupId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken);
    Task<ChatMessageDto> SendMessageAsync(Guid userId, Guid conversationId, SendChatMessageCommand command, CancellationToken cancellationToken);
}
