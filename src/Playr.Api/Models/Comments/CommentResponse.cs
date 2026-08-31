using Playr.Api.Models.Common;

namespace Playr.Api.Models.Comments;

public sealed record CommentResponse(
    Guid Id,
    Guid PostId,
    Guid AuthorId,
    string AuthorUsername,
    string AuthorDisplayName,
    string? AuthorAvatarUrl,
    string? AuthorActiveBadgeType,
    string? AuthorActiveBadgeLevel,
    string TextContent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    CommentReactionResponse Reactions,
    IReadOnlyList<MentionResponse> Mentions);
