using Microsoft.EntityFrameworkCore;
using Playr.Application.Steam;
using Playr.Domain.Steam;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Steam;

public sealed class SteamService(
    PlayrDbContext dbContext,
    SteamOpenIdService openIdService,
    SteamApiClient apiClient) : ISteamService
{
    public string BuildLinkRedirectUrl(string returnUrl, string realm) =>
        openIdService.BuildRedirectUrl(returnUrl, realm);

    public async Task<SteamAccountDto> CompleteLinkAsync(Guid userId, IReadOnlyDictionary<string, string> callbackQuery, CancellationToken cancellationToken)
    {
        var steamId = await openIdService.VerifyAndExtractSteamIdAsync(callbackQuery, cancellationToken)
            ?? throw new InvalidOperationException("Steam did not confirm the login (invalid or expired OpenID response).");

        var summary = await apiClient.GetPlayerSummaryAsync(steamId, cancellationToken);

        var account = await dbContext.SteamAccounts.FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
        if (account is null)
        {
            account = new SteamAccount { UserId = userId, SteamId = steamId, LinkedAt = DateTimeOffset.UtcNow };
            dbContext.SteamAccounts.Add(account);
        }
        else
        {
            account.SteamId = steamId;
        }

        account.DisplayName = summary?.PersonaName;
        account.AvatarUrl = summary?.AvatarFullUrl;
        account.IsPublic = summary?.IsProfilePublic ?? false;

        await dbContext.SaveChangesAsync(cancellationToken);

        await SyncGamesInternalAsync(account, cancellationToken);

        return ToDto(account);
    }

    public async Task UnlinkAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await dbContext.SteamAccounts.FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
        if (account is null)
        {
            return;
        }

        var games = dbContext.SteamOwnedGames.Where(g => g.UserId == userId);
        dbContext.SteamOwnedGames.RemoveRange(games);
        var achievements = dbContext.SteamAchievements.Where(a => a.UserId == userId);
        dbContext.SteamAchievements.RemoveRange(achievements);
        dbContext.SteamAccounts.Remove(account);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<SteamAccountDto?> GetAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await dbContext.SteamAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
        return account is null ? null : ToDto(account);
    }

    public async Task<IReadOnlyList<SteamGameDto>> GetGamesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await dbContext.SteamAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
        if (account is null || !account.IsPublic)
        {
            return [];
        }

        var games = await dbContext.SteamOwnedGames.AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.PlaytimeForeverMinutes)
            .ToListAsync(cancellationToken);

        return games.Select(g => new SteamGameDto(g.AppId, g.Name, g.IconUrl, g.PlaytimeForeverMinutes, g.PlaytimeRecentMinutes)).ToList();
    }

    public async Task SyncGamesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await dbContext.SteamAccounts.FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
        if (account is null)
        {
            return;
        }

        await SyncGamesInternalAsync(account, cancellationToken);
    }

    private static readonly TimeSpan AchievementCacheLifetime = TimeSpan.FromHours(1);

    public async Task<IReadOnlyList<SteamAchievementDto>> GetAchievementsAsync(Guid userId, long appId, CancellationToken cancellationToken)
    {
        var account = await dbContext.SteamAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
        if (account is null || !account.IsPublic)
        {
            return [];
        }

        var cached = await dbContext.SteamAchievements
            .Where(a => a.UserId == userId && a.AppId == appId)
            .ToListAsync(cancellationToken);

        var isStale = cached.Count == 0 || cached.Max(a => a.LastSyncedAt) < DateTimeOffset.UtcNow - AchievementCacheLifetime;
        if (isStale)
        {
            cached = await SyncAchievementsInternalAsync(userId, account.SteamId, appId, cancellationToken);
        }

        return cached
            .OrderByDescending(a => a.Achieved)
            .ThenBy(a => a.DisplayName)
            .Select(a => new SteamAchievementDto(a.ApiName, a.DisplayName, a.IconUrl, a.IconGrayUrl, a.Achieved, a.UnlockedAt))
            .ToList();
    }

    private async Task<List<SteamAchievement>> SyncAchievementsInternalAsync(Guid userId, string steamId, long appId, CancellationToken cancellationToken)
    {
        var results = await apiClient.GetAchievementsAsync(steamId, appId, cancellationToken);

        var existing = await dbContext.SteamAchievements
            .Where(a => a.UserId == userId && a.AppId == appId)
            .ToListAsync(cancellationToken);
        var existingByApiName = existing.ToDictionary(a => a.ApiName);

        var now = DateTimeOffset.UtcNow;
        var updated = new List<SteamAchievement>();
        foreach (var result in results)
        {
            if (!existingByApiName.TryGetValue(result.ApiName, out var row))
            {
                row = new SteamAchievement { Id = Guid.NewGuid(), UserId = userId, AppId = appId, ApiName = result.ApiName };
                dbContext.SteamAchievements.Add(row);
            }

            row.DisplayName = result.DisplayName;
            row.IconUrl = result.IconUrl;
            row.IconGrayUrl = result.IconGrayUrl;
            row.Achieved = result.Achieved;
            row.UnlockedAt = result.UnlockedAt;
            row.LastSyncedAt = now;
            updated.Add(row);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return updated;
    }

    private async Task SyncGamesInternalAsync(SteamAccount account, CancellationToken cancellationToken)
    {
        var summary = await apiClient.GetPlayerSummaryAsync(account.SteamId, cancellationToken);
        account.IsPublic = summary?.IsProfilePublic ?? false;

        var ownedGames = account.IsPublic
            ? await apiClient.GetOwnedGamesAsync(account.SteamId, cancellationToken)
            : [];

        var existing = await dbContext.SteamOwnedGames
            .Where(g => g.UserId == account.UserId)
            .ToListAsync(cancellationToken);
        var existingByAppId = existing.ToDictionary(g => g.AppId);

        var now = DateTimeOffset.UtcNow;
        foreach (var game in ownedGames)
        {
            if (existingByAppId.TryGetValue(game.AppId, out var row))
            {
                row.Name = game.Name;
                row.IconUrl = game.IconUrl;
                row.PlaytimeForeverMinutes = game.PlaytimeForeverMinutes;
                row.PlaytimeRecentMinutes = game.PlaytimeRecentMinutes;
                row.LastSyncedAt = now;
                existingByAppId.Remove(game.AppId);
            }
            else
            {
                dbContext.SteamOwnedGames.Add(new SteamOwnedGame
                {
                    Id = Guid.NewGuid(),
                    UserId = account.UserId,
                    AppId = game.AppId,
                    Name = game.Name,
                    IconUrl = game.IconUrl,
                    PlaytimeForeverMinutes = game.PlaytimeForeverMinutes,
                    PlaytimeRecentMinutes = game.PlaytimeRecentMinutes,
                    LastSyncedAt = now,
                });
            }
        }

        // Anything left in existingByAppId is no longer owned (or profile went private) - remove it.
        if (existingByAppId.Count > 0)
        {
            dbContext.SteamOwnedGames.RemoveRange(existingByAppId.Values);
        }

        account.LastSyncedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static SteamAccountDto ToDto(SteamAccount account) => new(
        account.UserId,
        account.SteamId,
        account.DisplayName,
        account.AvatarUrl,
        account.IsPublic,
        account.LinkedAt,
        account.LastSyncedAt);
}
