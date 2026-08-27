namespace Playr.Api.Models.Comments;

public sealed record CommentResponse(
    Guid Id,
    Guid PostId,
    Guid AuthorId,
    string AuthorUsername,
    string AuthorDisplayName,
    string? AuthorAvatarUrl,
    string TextContent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
