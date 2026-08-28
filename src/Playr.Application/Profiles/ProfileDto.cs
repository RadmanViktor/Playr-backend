using Playr.Application.Invitations;
using Playr.Domain.Profiles;

namespace Playr.Application.Profiles;

public sealed record ProfileDto(
    Guid UserId,
    string Username,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string? Region,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Platforms,
    IReadOnlyDictionary<string, string> ExternalLinks,
    IReadOnlyList<string> CurrentlyPlayingGames,
    ProfileStatus Status,
    Guid? LookingForGameId,
    string? LookingForGameName,
    PlayStyle? LookingForPlayStyle,
    string? LookingForGameNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    RelationshipStatus? RelationshipStatus = null,
    Guid? PendingInvitationId = null);
