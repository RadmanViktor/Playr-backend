using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Playr.Infrastructure.Steam;

public sealed record SteamOwnedGameResult(long AppId, string Name, string? IconUrl, int PlaytimeForeverMinutes, int PlaytimeRecentMinutes);

public sealed record SteamPlayerSummaryResult(string SteamId, string PersonaName, string? AvatarFullUrl, bool IsProfilePublic);

public sealed record SteamAchievementResult(string ApiName, string? DisplayName, string? IconUrl, string? IconGrayUrl, bool Achieved, DateTimeOffset? UnlockedAt);

/// <summary>
/// Typed client for the Steam Web API (https://api.steampowered.com).
/// </summary>
public sealed class SteamApiClient(HttpClient httpClient, IOptions<SteamOptions> options, ILogger<SteamApiClient> logger)
{
    private readonly SteamOptions _options = options.Value;

    public async Task<SteamPlayerSummaryResult?> GetPlayerSummaryAsync(string steamId, CancellationToken cancellationToken)
    {
        WarnIfApiKeyMissing();
        var url = $"/ISteamUser/GetPlayerSummaries/v2/?key={_options.ApiKey}&steamids={steamId}";
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Steam GetPlayerSummaries failed for steamId {SteamId} with status {StatusCode}.",
                steamId, (int)response.StatusCode);
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<PlayerSummariesEnvelope>(JsonOptions, cancellationToken);
        var player = payload?.Response?.Players?.FirstOrDefault();
        if (player is null)
        {
            return null;
        }

        // communityvisibilitystate == 3 means the profile is public.
        return new SteamPlayerSummaryResult(player.SteamId, player.PersonaName, player.AvatarFull, player.CommunityVisibilityState == 3);
    }

    /// <summary>
    /// Returns the user's owned games, or an empty list if the game details are private
    /// (Steam returns a response with no "games" property in that case).
    /// </summary>
    public async Task<IReadOnlyList<SteamOwnedGameResult>> GetOwnedGamesAsync(string steamId, CancellationToken cancellationToken)
    {
        WarnIfApiKeyMissing();
        var url = $"/IPlayerService/GetOwnedGames/v1/?key={_options.ApiKey}&steamid={steamId}&include_appinfo=true&include_played_free_games=true";
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Steam GetOwnedGames failed for steamId {SteamId} with status {StatusCode}.",
                steamId, (int)response.StatusCode);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<OwnedGamesEnvelope>(JsonOptions, cancellationToken);
        var games = payload?.Response?.Games;
        if (games is null)
        {
            return [];
        }

        return games.Select(g => new SteamOwnedGameResult(
            g.AppId,
            g.Name ?? $"App {g.AppId}",
            g.ImgIconUrl is { Length: > 0 }
                ? $"https://media.steampowered.com/steamcommunity/public/images/apps/{g.AppId}/{g.ImgIconUrl}.jpg"
                : null,
            g.PlaytimeForever,
            g.Playtime2Weeks)).ToList();
    }

    /// <summary>
    /// Returns the player's achievement unlock states for a specific game, merged with the game's
    /// achievement schema (display name/icons). Returns an empty list if the game has no stats/achievements,
    /// the profile's game details are private, or the request otherwise fails.
    /// </summary>
    public async Task<IReadOnlyList<SteamAchievementResult>> GetAchievementsAsync(string steamId, long appId, CancellationToken cancellationToken)
    {
        var playerAchievements = await GetPlayerAchievementsAsync(steamId, appId, cancellationToken);
        if (playerAchievements.Count == 0)
        {
            return [];
        }

        var schema = await GetSchemaAchievementsAsync(appId, cancellationToken);
        var schemaByApiName = schema.ToDictionary(s => s.ApiName);

        return playerAchievements.Select(a =>
        {
            schemaByApiName.TryGetValue(a.ApiName, out var schemaEntry);
            return new SteamAchievementResult(
                a.ApiName,
                schemaEntry?.DisplayName ?? a.ApiName,
                schemaEntry?.Icon,
                schemaEntry?.IconGray,
                a.Achieved == 1,
                a.UnlockTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(a.UnlockTime) : null);
        }).ToList();
    }

    private async Task<List<PlayerAchievement>> GetPlayerAchievementsAsync(string steamId, long appId, CancellationToken cancellationToken)
    {
        WarnIfApiKeyMissing();
        var url = $"/ISteamUserStats/GetPlayerAchievements/v1/?key={_options.ApiKey}&steamid={steamId}&appid={appId}";
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Steam returns 400 for games with no stats, and for private profiles.
            logger.LogWarning(
                "Steam GetPlayerAchievements failed for steamId {SteamId}, appId {AppId} with status {StatusCode}. " +
                "This is expected for private profiles or games without stats, but may also indicate a missing/invalid Steam API key.",
                steamId, appId, (int)response.StatusCode);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<PlayerAchievementsEnvelope>(JsonOptions, cancellationToken);
        return payload?.PlayerStats?.Achievements ?? [];
    }

    private async Task<List<SchemaAchievement>> GetSchemaAchievementsAsync(long appId, CancellationToken cancellationToken)
    {
        WarnIfApiKeyMissing();
        var url = $"/ISteamUserStats/GetSchemaForGame/v2/?key={_options.ApiKey}&appid={appId}";
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Steam GetSchemaForGame failed for appId {AppId} with status {StatusCode}.",
                appId, (int)response.StatusCode);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<SchemaEnvelope>(JsonOptions, cancellationToken);
        return payload?.Game?.AvailableGameStats?.Achievements ?? [];
    }

    private bool _hasWarnedAboutApiKey;

    private void WarnIfApiKeyMissing()
    {
        if (_hasWarnedAboutApiKey)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey) || _options.ApiKey == "CHANGE-ME")
        {
            _hasWarnedAboutApiKey = true;
            logger.LogWarning(
                "Steam:ApiKey is not configured (still set to the default placeholder or empty). " +
                "All Steam Web API calls will fail and silently be treated as 'no data' by callers.");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record PlayerSummariesEnvelope([property: JsonPropertyName("response")] PlayerSummariesResponse? Response);
    private sealed record PlayerSummariesResponse([property: JsonPropertyName("players")] List<PlayerSummary>? Players);
    private sealed record PlayerSummary(
        [property: JsonPropertyName("steamid")] string SteamId,
        [property: JsonPropertyName("personaname")] string PersonaName,
        [property: JsonPropertyName("avatarfull")] string? AvatarFull,
        [property: JsonPropertyName("communityvisibilitystate")] int CommunityVisibilityState);

    private sealed record OwnedGamesEnvelope([property: JsonPropertyName("response")] OwnedGamesResponse? Response);
    private sealed record OwnedGamesResponse([property: JsonPropertyName("games")] List<OwnedGame>? Games);
    private sealed record OwnedGame(
        [property: JsonPropertyName("appid")] long AppId,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("img_icon_url")] string? ImgIconUrl,
        [property: JsonPropertyName("playtime_forever")] int PlaytimeForever,
        [property: JsonPropertyName("playtime_2weeks")] int Playtime2Weeks);

    private sealed record PlayerAchievementsEnvelope([property: JsonPropertyName("playerstats")] PlayerAchievementsStats? PlayerStats);
    private sealed record PlayerAchievementsStats([property: JsonPropertyName("achievements")] List<PlayerAchievement>? Achievements);
    private sealed record PlayerAchievement(
        [property: JsonPropertyName("apiname")] string ApiName,
        [property: JsonPropertyName("achieved")] int Achieved,
        [property: JsonPropertyName("unlocktime")] long UnlockTime);

    private sealed record SchemaEnvelope([property: JsonPropertyName("game")] SchemaGame? Game);
    private sealed record SchemaGame([property: JsonPropertyName("availableGameStats")] SchemaGameStats? AvailableGameStats);
    private sealed record SchemaGameStats([property: JsonPropertyName("achievements")] List<SchemaAchievement>? Achievements);
    private sealed record SchemaAchievement(
        [property: JsonPropertyName("name")] string ApiName,
        [property: JsonPropertyName("displayName")] string? DisplayName,
        [property: JsonPropertyName("icon")] string? Icon,
        [property: JsonPropertyName("icongray")] string? IconGray);
}