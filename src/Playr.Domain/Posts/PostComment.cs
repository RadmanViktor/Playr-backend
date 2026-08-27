using Playr.Domain.Identity;

namespace Playr.Domain.Posts;

public sealed class PostComment
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public ApplicationUser Author { get; set; } = null!;
    public string TextContent { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
