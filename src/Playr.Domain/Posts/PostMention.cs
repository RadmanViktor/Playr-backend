using Playr.Domain.Identity;

namespace Playr.Domain.Posts;

public sealed class PostMention
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;
    public Guid MentionedUserId { get; set; }
    public ApplicationUser MentionedUser { get; set; } = null!;
    public string UsernameAtTimeOfPosting { get; set; } = string.Empty;
}
