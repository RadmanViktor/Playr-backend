using Playr.Application.Common;

namespace Playr.Application.Comments;

public interface ICommentService
{
    Task<CommentDto> CreateAsync(Guid postId, Guid authorId, CreateCommentCommand command, CancellationToken cancellationToken);
    Task<PagedResult<CommentDto>> GetPagedAsync(Guid postId, int skip, int take, CancellationToken cancellationToken);
    Task<CommentDto> UpdateAsync(Guid postId, Guid commentId, Guid requesterId, UpdateCommentCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(Guid postId, Guid commentId, Guid requesterId, CancellationToken cancellationToken);
}
