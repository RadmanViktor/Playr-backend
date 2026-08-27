namespace Playr.Domain.Posts;

public sealed class PostMedia
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;
    public string Url { get; set; } = string.Empty;
    public PostMediaType MediaType { get; set; }
    public int SortOrder { get; set; }
}
