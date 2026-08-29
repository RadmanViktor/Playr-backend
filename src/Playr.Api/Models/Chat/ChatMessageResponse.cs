using Playr.Domain.Chat;

namespace Playr.Api.Models.Chat;

public sealed record ChatMessageResponse(
    Guid Id,
    Guid ConversationId,
    Guid SenderUserId,
    string SenderUsername,
    string SenderDisplayName,
    string? SenderAvatarUrl,
    string Body,
    string? MediaUrl,
    ChatMediaType? MediaType,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);
