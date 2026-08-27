namespace Playr.Api.Models.Comments;

public sealed record PagedCommentResponse(
    IReadOnlyList<CommentResponse> Items,
    int TotalCount,
    bool HasMore);
