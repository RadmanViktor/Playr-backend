using Microsoft.EntityFrameworkCore;
using Playr.Application.Games;
using Playr.Domain.Games;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Rawg;

namespace Playr.Infrastructure.Games;

public sealed class GameService(PlayrDbContext dbContext, RawgApiClient rawgApiClient) : IGameService
{
    public async Task<IReadOnlyList<GameDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Games
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .Select(g => new GameDto(g.Id, g.Name, g.CoverImageUrl, g.Genre))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExternalGameSearchResultDto>> SearchExternalAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var results = await rawgApiClient.SearchGamesAsync(query, cancellationToken);
        return results
            .Select(r => new ExternalGameSearchResultDto(r.RawgId, r.Name, r.CoverImageUrl, r.Genre))
            .ToList();
    }

    public async Task<(GameDto Game, bool Created)> CreateFromExternalAsync(CreateGameCommand command, CancellationToken cancellationToken)
    {
        if (command.RawgId is { } rawgId)
        {
            var existing = await dbContext.Games
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.RawgId == rawgId, cancellationToken);
            if (existing is not null)
            {
                return (new GameDto(existing.Id, existing.Name, existing.CoverImageUrl, existing.Genre), false);
            }
        }

        var game = new Game
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            CoverImageUrl = command.CoverImageUrl,
            Genre = command.Genre,
            RawgId = command.RawgId,
        };

        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (new GameDto(game.Id, game.Name, game.CoverImageUrl, game.Genre), true);
    }
}
