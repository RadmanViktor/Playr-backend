namespace Playr.Application.Games;

public sealed record FavoriteGameDto(
    Guid GameId,
    string GameName,
    string? GameCoverImageUrl,
    string? Genre,
    DateTimeOffset CreatedAt);

public interface IFavoriteGameService
{
    Task<IReadOnlyList<FavoriteGameDto>> GetForUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<FavoriteGameDto> AddAsync(Guid userId, Guid gameId, CancellationToken cancellationToken);
    Task RemoveAsync(Guid userId, Guid gameId, CancellationToken cancellationToken);
}
