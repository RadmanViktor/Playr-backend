namespace Playr.Api.Models.Games;

public sealed record GameResponse(
    Guid Id,
    string Name,
    string? CoverImageUrl,
    string? Genre);

public sealed record ExternalGameSearchResponse(
    long RawgId,
    string Name,
    string? CoverImageUrl,
    string? Genre);

public sealed record CreateGameRequest(
    long? RawgId,
    string Name,
    string? CoverImageUrl,
    string? Genre);
