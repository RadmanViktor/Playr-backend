namespace Playr.Application.Games;

public sealed record GameDto(
    Guid Id,
    string Name,
    string? CoverImageUrl,
    string? Genre);
