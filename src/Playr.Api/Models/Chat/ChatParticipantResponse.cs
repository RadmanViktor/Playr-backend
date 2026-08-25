namespace Playr.Api.Models.Chat;

public sealed record ChatParticipantResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);
