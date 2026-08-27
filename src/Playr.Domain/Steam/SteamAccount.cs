using Playr.Domain.Identity;

namespace Playr.Domain.Steam;

/// <summary>
/// Represents a Playr user's linked Steam account, established via Steam OpenID.
/// </summary>
public sealed class SteamAccount
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public string SteamId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Whether the linked Steam profile/game details are publicly visible to the Steam Web API.
    /// When false, game data cannot be synced.
    /// </summary>
    public bool IsPublic { get; set; }

    public DateTimeOffset LinkedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSyncedAt { get; set; }
}
