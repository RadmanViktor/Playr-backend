using Playr.Domain.Profiles;

namespace Playr.Api.Models.Profiles;

public sealed record ProfileResponse(
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
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? RelationshipStatus = null);
