using Playr.Domain.Profiles;

namespace Playr.Api.Models.Profiles;

public sealed record LookingForGamePlayerResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    Guid? LookingForGameId,
    string? LookingForGameName,
    PlayStyle? LookingForPlayStyle,
    string? LookingForGameNote,
    string RelationshipStatus,
    Guid? PendingInvitationId = null,
    int? PreferredMinAge = null,
    int? PreferredMaxAge = null,
    bool VoiceChatEnabled = false);
