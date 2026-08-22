namespace Playr.Application.Games;

public interface IGameService
{
    Task<IReadOnlyList<GameDto>> GetAllAsync(CancellationToken cancellationToken);
}
