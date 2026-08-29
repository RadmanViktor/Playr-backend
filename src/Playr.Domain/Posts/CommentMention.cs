using Playr.Domain.Identity;

namespace Playr.Domain.Posts;

public sealed class CommentMention
{
    public Guid Id { get; set; }
    public Guid CommentId { get; set; }
    public PostComment Comment { get; set; } = null!;
    public Guid MentionedUserId { get; set; }
    public ApplicationUser MentionedUser { get; set; } = null!;
    public string UsernameAtTimeOfPosting { get; set; } = string.Empty;
}
