using Playr.Domain.Chat;

namespace Playr.Application.Chat;

public sealed record ConversationDto(
    Guid Id,
    ConversationType Type,
    string? Title,
    ChatParticipantDto? OtherParticipant,
    string? LastMessage,
    DateTimeOffset? LastMessageAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ChatParticipantDto> Participants);
