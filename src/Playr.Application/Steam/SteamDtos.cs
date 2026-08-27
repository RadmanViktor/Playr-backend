namespace Playr.Application.Steam;

public sealed record SteamAccountDto(
    Guid UserId,
    string SteamId,
    string? DisplayName,
    string? AvatarUrl,
    bool IsPublic,
    DateTimeOffset LinkedAt,
    DateTimeOffset? LastSyncedAt);

public sealed record SteamGameDto(
    long AppId,
    string Name,
    string? IconUrl,
    int PlaytimeForeverMinutes,
    int PlaytimeRecentMinutes);

public sealed record SteamAchievementDto(
    string ApiName,
    string? DisplayName,
    string? IconUrl,
    string? IconGrayUrl,
    bool Achieved,
    DateTimeOffset? UnlockedAt);
