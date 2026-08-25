namespace Playr.Application.Chat;

public sealed record ConversationDto(
    Guid Id,
    ChatParticipantDto OtherParticipant,
    string? LastMessage,
    DateTimeOffset? LastMessageAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
