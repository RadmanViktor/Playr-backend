namespace Playr.Api.Models.Games;

public sealed record FavoriteGameResponse(
    Guid GameId,
    string GameName,
    string? GameCoverImageUrl,
    string? Genre,
    DateTimeOffset CreatedAt);

public sealed record AddFavoriteGameRequest(Guid GameId);
