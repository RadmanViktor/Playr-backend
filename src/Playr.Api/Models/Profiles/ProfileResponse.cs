using Playr.Domain.Profiles;

namespace Playr.Api.Models.Profiles;

public sealed record ProfileResponse(
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
    PlaystylePreference? PlaystylePreference,
    UsuallyPlayingWith? UsuallyPlayingWith,
    IReadOnlyList<string> TypicalPlayTimes,
    bool HasCompletedOnboarding,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? RelationshipStatus = null,
    Guid? PendingInvitationId = null);
