namespace Playr.Application.Posts;

public interface IPostService
{
    Task<PostDto> CreateAsync(Guid authorId, CreatePostCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<PostDto>> GetFeedAsync(Guid? currentUserId, CancellationToken cancellationToken);
    Task<PostDto> UpdateAsync(Guid postId, Guid requesterId, UpdatePostCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(Guid postId, Guid requesterId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PostDto>> GetByUsernameAsync(string username, Guid? currentUserId, CancellationToken cancellationToken);
    Task<(int LikesCount, bool Liked)> ToggleLikeAsync(Guid postId, Guid userId, CancellationToken cancellationToken);
}
