namespace Playr.Application.Games;

public sealed record GameLibraryEntryDto(
    Guid GameId,
    string GameName,
    string? GameCoverImageUrl,
    string? Genre,
    int? Rating,
    string? ReviewText,
    DateTimeOffset AddedAt,
    DateTimeOffset UpdatedAt);

public interface IGameLibraryService
{
    Task<IReadOnlyList<GameLibraryEntryDto>> GetLibraryAsync(Guid userId, CancellationToken cancellationToken);

    Task<GameLibraryEntryDto> AddGameAsync(Guid userId, Guid gameId, CancellationToken cancellationToken);

    Task<GameLibraryEntryDto> RateGameAsync(
        Guid userId, Guid gameId, int rating, string? reviewText, CancellationToken cancellationToken);

    Task RemoveGameAsync(Guid userId, Guid gameId, CancellationToken cancellationToken);
}
