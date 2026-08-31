using Playr.Application.Common;

namespace Playr.Application.Comments;

public sealed record CommentDto(
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
    CommentReactionSummary Reactions,
    IReadOnlyList<MentionDto> Mentions);
