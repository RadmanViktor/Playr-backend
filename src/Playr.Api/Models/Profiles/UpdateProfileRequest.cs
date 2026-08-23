using System.ComponentModel.DataAnnotations;

namespace Playr.Api.Models.Profiles;

public sealed record UpdateProfileRequest(
    [Required, StringLength(64, MinimumLength = 1)] string DisplayName,
    [StringLength(500)] string? Bio,
    [StringLength(500)] string? AvatarUrl,
    [StringLength(64)] string? Region,
    IReadOnlyList<string>? Languages,
    IReadOnlyList<string>? Platforms,
    IReadOnlyDictionary<string, string>? ExternalLinks,
    IReadOnlyList<string>? CurrentlyPlayingGames);
