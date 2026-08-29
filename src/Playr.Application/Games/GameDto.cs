namespace Playr.Application.Games;

public sealed record GameDto(
    Guid Id,
    string Name,
    string? CoverImageUrl,
    string? Genre);

public sealed record ExternalGameSearchResultDto(
    long RawgId,
    string Name,
    string? CoverImageUrl,
    string? Genre);

public sealed record CreateGameCommand(
    long? RawgId,
    string Name,
    string? CoverImageUrl,
    string? Genre);
