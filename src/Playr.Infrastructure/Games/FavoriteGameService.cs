using Microsoft.EntityFrameworkCore;
using Playr.Application.Games;
using Playr.Domain.Games;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Games;

public sealed class FavoriteGameService(PlayrDbContext dbContext) : IFavoriteGameService
{
    private const int MaxFavoritesPerUser = 6;

    public async Task<IReadOnlyList<FavoriteGameDto>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.UserFavoriteGames
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .Include(f => f.Game)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FavoriteGameDto(f.GameId, f.Game.Name, f.Game.CoverImageUrl, f.Game.Genre, f.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<FavoriteGameDto> AddAsync(Guid userId, Guid gameId, CancellationToken cancellationToken)
    {
        var game = await dbContext.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken)
            ?? throw new InvalidOperationException("The selected game was not found.");

        var alreadyFavorited = await dbContext.UserFavoriteGames
            .AnyAsync(f => f.UserId == userId && f.GameId == gameId, cancellationToken);
        if (alreadyFavorited)
        {
            throw new InvalidOperationException("This game is already in your favorites.");
        }

        var existingCount = await dbContext.UserFavoriteGames.CountAsync(f => f.UserId == userId, cancellationToken);
        if (existingCount >= MaxFavoritesPerUser)
        {
            throw new InvalidOperationException($"You can only mark up to {MaxFavoritesPerUser} games as favorites.");
        }

        var now = DateTimeOffset.UtcNow;
        var entry = new UserFavoriteGame
        {
            UserId = userId,
            GameId = gameId,
            CreatedAt = now,
        };
        dbContext.UserFavoriteGames.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new FavoriteGameDto(game.Id, game.Name, game.CoverImageUrl, game.Genre, now);
    }

    public async Task RemoveAsync(Guid userId, Guid gameId, CancellationToken cancellationToken)
    {
        var entry = await dbContext.UserFavoriteGames
            .FirstOrDefaultAsync(f => f.UserId == userId && f.GameId == gameId, cancellationToken)
            ?? throw new InvalidOperationException("This game is not in your favorites.");

        dbContext.UserFavoriteGames.Remove(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
