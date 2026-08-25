namespace Playr.Application.Chat;

public sealed record ChatParticipantDto(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);
