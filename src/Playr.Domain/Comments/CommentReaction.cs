using Playr.Domain.Identity;
using Playr.Domain.Posts;

namespace Playr.Domain.Comments;

public sealed class CommentReaction
{
    public Guid CommentId { get; set; }
    public PostComment Comment { get; set; } = null!;
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public ReactionType Type { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
