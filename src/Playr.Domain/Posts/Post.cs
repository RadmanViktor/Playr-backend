using Playr.Domain.Identity;
using Playr.Domain.Games;

namespace Playr.Domain.Posts;

public sealed class Post
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public ApplicationUser Author { get; set; } = null!;
    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;
    public string TextContent { get; set; } = string.Empty;
    public PostMood? Mood { get; set; }
    public List<PostMedia> Media { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}
