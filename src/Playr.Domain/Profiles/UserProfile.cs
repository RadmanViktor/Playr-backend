using Playr.Domain.Identity;

namespace Playr.Domain.Profiles;

public sealed class UserProfile
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Region { get; set; }
    public List<string> Languages { get; set; } = [];
    public List<string> Platforms { get; set; } = [];
    public Dictionary<string, string> ExternalLinks { get; set; } = [];
    public List<string> CurrentlyPlayingGames { get; set; } = [];
    public bool LookingForPlayers { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
