namespace Playr.Api.Models.Games;

public sealed record GameResponse(
    Guid Id,
    string Name,
    string? CoverImageUrl,
    string? Genre);
