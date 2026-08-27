namespace Playr.Application.Posts;

public sealed record PostMediaDto(
    Guid Id,
    string Url,
    string MediaType,
    int SortOrder);

public sealed record PostDto(
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
    IReadOnlyList<PostMediaDto> Media,
    DateTimeOffset CreatedAt,
    int LikesCount,
    bool LikedByCurrentUser,
    int CommentsCount);
