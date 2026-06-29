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
    bool LookingForPlayers,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
