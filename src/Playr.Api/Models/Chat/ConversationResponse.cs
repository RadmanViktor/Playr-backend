namespace Playr.Api.Models.Chat;

public sealed record ConversationResponse(
    Guid Id,
    string Type,
    string? Title,
    ChatParticipantResponse? OtherParticipant,
    string? LastMessage,
    DateTimeOffset? LastMessageAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ChatParticipantResponse> Participants,
    Guid? LfgGroupId);
