namespace Playr.Application.Posts;

public interface IPostService
{
    Task<PostDto> CreateAsync(Guid authorId, CreatePostCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<PostDto>> GetFeedAsync(CancellationToken cancellationToken);
}
