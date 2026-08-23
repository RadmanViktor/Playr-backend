using Playr.Domain.Invitations;

namespace Playr.Application.Invitations;

public sealed record InvitationDto(
    Guid Id,
    Guid SenderUserId,
    string SenderUsername,
    string SenderDisplayName,
    string? SenderAvatarUrl,
    Guid RecipientUserId,
    string RecipientUsername,
    string RecipientDisplayName,
    string? RecipientAvatarUrl,
    string Message,
    InvitationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt);
