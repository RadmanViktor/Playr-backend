namespace Playr.Application.Posts;

public interface IPostService
{
    Task<PostDto> CreateAsync(Guid authorId, CreatePostCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<PostDto>> GetFeedAsync(CancellationToken cancellationToken);
    Task<PostDto> UpdateAsync(Guid postId, Guid requesterId, UpdatePostCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(Guid postId, Guid requesterId, CancellationToken cancellationToken);
}
