namespace Playr.Api.Models.Comments;

public sealed record CommentReactionResponse(ReactionCountsResponse Counts, string? CurrentUserReaction);
