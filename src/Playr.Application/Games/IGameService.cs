namespace Playr.Application.Games;

public interface IGameService
{
    Task<IReadOnlyList<GameDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalGameSearchResultDto>> SearchExternalAsync(string query, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new game from an external search result, or returns the existing catalog entry
    /// if a game with the same RawgId already exists. Returns whether a new game was created.
    /// </summary>
    Task<(GameDto Game, bool Created)> CreateFromExternalAsync(CreateGameCommand command, CancellationToken cancellationToken);
}
