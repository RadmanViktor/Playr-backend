namespace Playr.Application.Steam;

public interface ISteamService
{
    /// <summary>
    /// Builds the Steam OpenID redirect URL the user should be sent to in order to link their account.
    /// </summary>
    string BuildLinkRedirectUrl(string returnUrl, string realm);

    /// <summary>
    /// Verifies the OpenID callback query parameters from Steam, and if valid, links (or updates)
    /// the Steam account for the given user and triggers an initial sync.
    /// </summary>
    Task<SteamAccountDto> CompleteLinkAsync(Guid userId, IReadOnlyDictionary<string, string> callbackQuery, CancellationToken cancellationToken);

    Task UnlinkAsync(Guid userId, CancellationToken cancellationToken);

    Task<SteamAccountDto?> GetAccountAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the cached game library for a user. Visible to any caller (own account or another user's
    /// profile), returns an empty list if the user has no linked account or their profile is private.
    /// </summary>
    Task<IReadOnlyList<SteamGameDto>> GetGamesAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Re-fetches the owned games library from Steam and updates the cache for the given user.
    /// </summary>
    Task SyncGamesAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the cached achievement states for a user's game, refreshing from Steam first if the
    /// cache is missing or stale. Visible to any caller, returns an empty list if the user has no
    /// linked account, the profile is private, or the game has no achievements.
    /// </summary>
    Task<IReadOnlyList<SteamAchievementDto>> GetAchievementsAsync(Guid userId, long appId, CancellationToken cancellationToken);
}
