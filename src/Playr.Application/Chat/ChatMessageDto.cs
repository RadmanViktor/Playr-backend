namespace Playr.Application.Chat;

public sealed record ChatMessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderUserId,
    string SenderUsername,
    string SenderDisplayName,
    string? SenderAvatarUrl,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);
