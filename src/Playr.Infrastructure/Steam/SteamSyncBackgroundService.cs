using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Steam;

/// <summary>
/// Periodically re-syncs every linked Steam account's game library. Runs once at startup
/// and then on a fixed interval configured via Steam:SyncIntervalHours.
/// </summary>
public sealed class SteamSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<SteamOptions> options,
    ILogger<SteamSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, options.Value.SyncIntervalHours));

        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncAllAsync(stoppingToken);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlayrDbContext>();
        var steamService = scope.ServiceProvider.GetRequiredService<Playr.Application.Steam.ISteamService>();

        var userIds = await dbContext.SteamAccounts.AsNoTracking()
            .Select(a => a.UserId)
            .ToListAsync(cancellationToken);

        foreach (var userId in userIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await steamService.SyncGamesAsync(userId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to sync Steam library for user {UserId}", userId);
            }

            // Light throttling to stay well within Steam's rate limits.
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }
}
