using Microsoft.EntityFrameworkCore;
using Playr.Application.Games;
using Playr.Domain.Games;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Games;

public sealed class GameLibraryService(PlayrDbContext dbContext) : IGameLibraryService
{
    private const int MaxReviewLength = 1000;

    public async Task<IReadOnlyList<GameLibraryEntryDto>> GetLibraryAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.UserGameLibraryEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Include(e => e.Game)
            .OrderByDescending(e => e.AddedAt)
            .Select(e => new GameLibraryEntryDto(
                e.GameId, e.Game.Name, e.Game.CoverImageUrl, e.Game.Genre, e.Rating, e.ReviewText, e.AddedAt, e.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<GameLibraryEntryDto> AddGameAsync(Guid userId, Guid gameId, CancellationToken cancellationToken)
    {
        var game = await dbContext.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken)
            ?? throw new InvalidOperationException("The selected game was not found.");

        var alreadyInLibrary = await dbContext.UserGameLibraryEntries
            .AnyAsync(e => e.UserId == userId && e.GameId == gameId, cancellationToken);
        if (alreadyInLibrary)
        {
            throw new InvalidOperationException("This game is already in your library.");
        }

        var now = DateTimeOffset.UtcNow;
        var entry = new UserGameLibraryEntry
        {
            UserId = userId,
            GameId = gameId,
            AddedAt = now,
            UpdatedAt = now,
        };
        dbContext.UserGameLibraryEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new GameLibraryEntryDto(game.Id, game.Name, game.CoverImageUrl, game.Genre, null, null, now, now);
    }

    public async Task<GameLibraryEntryDto> RateGameAsync(
        Guid userId, Guid gameId, int rating, string? reviewText, CancellationToken cancellationToken)
    {
        if (rating is < 1 or > 5)
        {
            throw new InvalidOperationException("Rating must be between 1 and 5.");
        }

        var trimmedReview = NormalizeOptionalText(reviewText);

        var entry = await dbContext.UserGameLibraryEntries
            .Include(e => e.Game)
            .FirstOrDefaultAsync(e => e.UserId == userId && e.GameId == gameId, cancellationToken)
            ?? throw new InvalidOperationException("Add this game to your library before rating it.");

        entry.Rating = rating;
        entry.ReviewText = trimmedReview;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new GameLibraryEntryDto(
            entry.GameId, entry.Game.Name, entry.Game.CoverImageUrl, entry.Game.Genre,
            entry.Rating, entry.ReviewText, entry.AddedAt, entry.UpdatedAt);
    }

    public async Task RemoveGameAsync(Guid userId, Guid gameId, CancellationToken cancellationToken)
    {
        var entry = await dbContext.UserGameLibraryEntries
            .FirstOrDefaultAsync(e => e.UserId == userId && e.GameId == gameId, cancellationToken)
            ?? throw new InvalidOperationException("This game is not in your library.");

        dbContext.UserGameLibraryEntries.Remove(entry);
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

        if (trimmed.Length > MaxReviewLength)
        {
            throw new InvalidOperationException($"Review text cannot be longer than {MaxReviewLength} characters.");
        }

        return trimmed;
    }
}
