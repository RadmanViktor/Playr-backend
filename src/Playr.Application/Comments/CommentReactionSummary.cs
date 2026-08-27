using Playr.Domain.Comments;

namespace Playr.Application.Comments;

public sealed record CommentReactionSummary(ReactionCounts Counts, ReactionType? CurrentUserReaction);
