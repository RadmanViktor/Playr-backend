namespace Playr.Api.Models.Invitations;

public sealed record InvitationResponse(
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
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt);
