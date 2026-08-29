namespace Playr.Api.Models.Games;

public sealed record GameLibraryEntryResponse(
    Guid GameId,
    string GameName,
    string? GameCoverImageUrl,
    string? Genre,
    int? Rating,
    string? ReviewText,
    DateTimeOffset AddedAt,
    DateTimeOffset UpdatedAt);

public sealed record AddGameToLibraryRequest(Guid GameId);

public sealed record RateGameRequest(int Rating, string? ReviewText);
