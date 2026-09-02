using Playr.Domain.Profiles;

namespace Playr.Application.Profiles;

public sealed record PublicLookingForGameSummaryDto(
    int TotalCount,
    PublicLookingForGameFeaturedGameDto? FeaturedGame,
    IReadOnlyList<PublicLookingForGamePlayerDto> Players);

public sealed record PublicLookingForGameFeaturedGameDto(
    string Name,
    string? CoverImageUrl,
    int PlayerCount);

public sealed record PublicLookingForGamePlayerDto(
    string Username,
    string DisplayName,
    string? AvatarUrl,
    string GameName,
    PlayStyle PlayStyle);
