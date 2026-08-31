namespace Playr.Application.Games;

public sealed record PlayingNowDto(
    Guid GameId,
    string GameName,
    string? GameCoverImageUrl,
    string? StatusText,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public interface IPlayingNowService
{
    Task<IReadOnlyList<PlayingNowDto>> GetForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Adds or updates a "playing now" entry for the given game.</summary>
    Task<PlayingNowDto> SetAsync(Guid userId, Guid gameId, string? statusText, CancellationToken cancellationToken);

    Task RemoveAsync(Guid userId, Guid gameId, CancellationToken cancellationToken);
}
