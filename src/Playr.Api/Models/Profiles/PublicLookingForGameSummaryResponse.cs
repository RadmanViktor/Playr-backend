using Playr.Domain.Profiles;

namespace Playr.Api.Models.Profiles;

public sealed record PublicLookingForGameSummaryResponse(
    int TotalCount,
    PublicLookingForGameFeaturedGameResponse? FeaturedGame,
    IReadOnlyList<PublicLookingForGamePlayerResponse> Players);

public sealed record PublicLookingForGameFeaturedGameResponse(
    string Name,
    string? CoverImageUrl,
    int PlayerCount);

public sealed record PublicLookingForGamePlayerResponse(
    string Username,
    string DisplayName,
    string? AvatarUrl,
    string GameName,
    PlayStyle PlayStyle);
