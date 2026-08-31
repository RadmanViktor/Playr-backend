namespace Playr.Application.Profiles;

public sealed record UpdateProfileCommand(
    string DisplayName,
    string? Bio,
    string? Region,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Platforms,
    IReadOnlyList<string> Genres,
    IReadOnlyDictionary<string, string> ExternalLinks);
