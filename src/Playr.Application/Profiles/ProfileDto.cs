using Playr.Application.Invitations;
using Playr.Domain.Profiles;

namespace Playr.Application.Profiles;

public sealed record ProfileDto(
    Guid UserId,
    string Username,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string? CoverImageUrl,
    double CoverImagePositionX,
    double CoverImagePositionY,
    string? Region,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Platforms,
    IReadOnlyList<string> Genres,
    IReadOnlyDictionary<string, string> ExternalLinks,
    ProfileStatus Status,
    Guid? LookingForGameId,
    string? LookingForGameName,
    PlayStyle? LookingForPlayStyle,
    string? LookingForGameNote,
    IReadOnlyList<string> TypicalPlayTimes,
    bool HasCompletedOnboarding,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ActiveBadgeType,
    string? ActiveBadgeLevel,
    RelationshipStatus? RelationshipStatus = null,
    Guid? PendingInvitationId = null,
    string? DiscordUsername = null,
    int? LookingForPreferredMinAge = null,
    int? LookingForPreferredMaxAge = null,
    bool LookingForVoiceChatEnabled = false);
