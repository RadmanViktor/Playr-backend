namespace Playr.Api.Models.Posts;

using Playr.Api.Models.Common;

public sealed record PostMediaResponse(
    Guid Id,
    string Url,
    string MediaType,
    int SortOrder);

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
    string Scope,
    IReadOnlyList<PostMediaResponse> Media,
    DateTimeOffset CreatedAt,
    int LikesCount,
    bool LikedByCurrentUser,
    int CommentsCount,
    IReadOnlyList<MentionResponse> Mentions);
