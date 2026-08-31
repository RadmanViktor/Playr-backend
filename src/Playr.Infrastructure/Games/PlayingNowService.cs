using Microsoft.EntityFrameworkCore;
using Playr.Application.Games;
using Playr.Domain.Games;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Games;

public sealed class PlayingNowService(PlayrDbContext dbContext) : IPlayingNowService
{
    private const int MaxStatusTextLength = 200;
    private const int MaxEntriesPerUser = 10;

    public async Task<IReadOnlyList<PlayingNowDto>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.UserPlayingNows
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Include(p => p.Game)
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new PlayingNowDto(p.GameId, p.Game.Name, p.Game.CoverImageUrl, p.StatusText, p.CreatedAt, p.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<PlayingNowDto> SetAsync(Guid userId, Guid gameId, string? statusText, CancellationToken cancellationToken)
    {
        var game = await dbContext.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken)
            ?? throw new InvalidOperationException("The selected game was not found.");

        var normalizedStatus = NormalizeOptionalText(statusText);
        var now = DateTimeOffset.UtcNow;

        var entry = await dbContext.UserPlayingNows
            .FirstOrDefaultAsync(p => p.UserId == userId && p.GameId == gameId, cancellationToken);

        if (entry is null)
        {
            var existingCount = await dbContext.UserPlayingNows.CountAsync(p => p.UserId == userId, cancellationToken);
            if (existingCount >= MaxEntriesPerUser)
            {
                throw new InvalidOperationException($"You can only mark up to {MaxEntriesPerUser} games as playing now.");
            }

            entry = new UserPlayingNow
            {
                UserId = userId,
                GameId = gameId,
                StatusText = normalizedStatus,
                CreatedAt = now,
                UpdatedAt = now,
            };
            dbContext.UserPlayingNows.Add(entry);
        }
        else
        {
            entry.StatusText = normalizedStatus;
            entry.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new PlayingNowDto(gameId, game.Name, game.CoverImageUrl, normalizedStatus, entry.CreatedAt, entry.UpdatedAt);
    }

    public async Task RemoveAsync(Guid userId, Guid gameId, CancellationToken cancellationToken)
    {
        var entry = await dbContext.UserPlayingNows
            .FirstOrDefaultAsync(p => p.UserId == userId && p.GameId == gameId, cancellationToken);
        if (entry is null)
        {
            return;
        }

        dbContext.UserPlayingNows.Remove(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > MaxStatusTextLength)
        {
            throw new InvalidOperationException($"Status text cannot be longer than {MaxStatusTextLength} characters.");
        }

        return trimmed;
    }
}
