namespace Playr.Api.Models.Games;

public sealed record PlayingNowResponse(
    Guid GameId,
    string GameName,
    string? GameCoverImageUrl,
    string? StatusText,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SetPlayingNowRequest(Guid GameId, string? StatusText);
