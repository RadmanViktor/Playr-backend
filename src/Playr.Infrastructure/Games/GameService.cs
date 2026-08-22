using Microsoft.EntityFrameworkCore;
using Playr.Application.Games;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Games;

public sealed class GameService(PlayrDbContext dbContext) : IGameService
{
    public async Task<IReadOnlyList<GameDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Games
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .Select(g => new GameDto(g.Id, g.Name, g.CoverImageUrl, g.Genre))
            .ToListAsync(cancellationToken);
    }
}
