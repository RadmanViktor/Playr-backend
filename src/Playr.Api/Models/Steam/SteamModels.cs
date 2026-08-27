namespace Playr.Api.Models.Steam;

public sealed record SteamAccountResponse(
    Guid UserId,
    string SteamId,
    string? DisplayName,
    string? AvatarUrl,
    bool IsPublic,
    DateTimeOffset LinkedAt,
    DateTimeOffset? LastSyncedAt);

public sealed record SteamGameResponse(
    long AppId,
    string Name,
    string? IconUrl,
    int PlaytimeForeverMinutes,
    int PlaytimeRecentMinutes);

public sealed record SteamAchievementResponse(
    string ApiName,
    string? DisplayName,
    string? IconUrl,
    string? IconGrayUrl,
    bool Achieved,
    DateTimeOffset? UnlockedAt);

public sealed record SteamLinkStartResponse(string RedirectUrl);
