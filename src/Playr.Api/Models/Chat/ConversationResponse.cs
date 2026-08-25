namespace Playr.Api.Models.Chat;

public sealed record ConversationResponse(
    Guid Id,
    ChatParticipantResponse OtherParticipant,
    string? LastMessage,
    DateTimeOffset? LastMessageAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
