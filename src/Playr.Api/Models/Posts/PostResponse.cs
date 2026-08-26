namespace Playr.Api.Models.Posts;

public sealed record PostResponse(
    Guid Id,
    Guid AuthorId,
    string AuthorUsername,
    string AuthorDisplayName,
    string? AuthorAvatarUrl,
    Guid GameId,
    string GameName,
    string? GameCoverImageUrl,
    string TextContent,
    string? Mood,
    string? MediaUrl,
    string? MediaType,
    DateTimeOffset CreatedAt,
    int LikesCount,
    bool LikedByCurrentUser);
