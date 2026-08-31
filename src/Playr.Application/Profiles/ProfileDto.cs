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
    RelationshipStatus? RelationshipStatus = null,
    Guid? PendingInvitationId = null);
