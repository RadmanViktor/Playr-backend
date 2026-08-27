using Playr.Application.Invitations;
using Playr.Domain.Profiles;

namespace Playr.Application.Profiles;

public sealed record LookingForGamePlayerDto(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    Guid? LookingForGameId,
    string? LookingForGameName,
    PlayStyle? LookingForPlayStyle,
    RelationshipStatus RelationshipStatus,
    Guid? PendingInvitationId = null);
