namespace Playr.Application.Posts;

using Playr.Application.Common;

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
    string? AuthorActiveBadgeType,
    string? AuthorActiveBadgeLevel,
    Guid GameId,
    string GameName,
    string? GameCoverImageUrl,
    string TextContent,
    string? Mood,
    string Scope,
    IReadOnlyList<PostMediaDto> Media,
    DateTimeOffset CreatedAt,
    int LikesCount,
    bool LikedByCurrentUser,
    int CommentsCount,
    IReadOnlyList<MentionDto> Mentions);
