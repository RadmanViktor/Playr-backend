using Playr.Application.Common;
using Playr.Domain.Comments;

namespace Playr.Application.Comments;

public interface ICommentService
{
    Task<CommentDto> CreateAsync(Guid postId, Guid authorId, CreateCommentCommand command, CancellationToken cancellationToken);
    Task<PagedResult<CommentDto>> GetPagedAsync(Guid postId, Guid? currentUserId, int skip, int take, CancellationToken cancellationToken);
    Task<CommentDto> UpdateAsync(Guid postId, Guid commentId, Guid requesterId, UpdateCommentCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(Guid postId, Guid commentId, Guid requesterId, CancellationToken cancellationToken);
    Task<CommentReactionSummary> SetReactionAsync(Guid postId, Guid commentId, Guid userId, ReactionType type, CancellationToken cancellationToken);
    Task<CommentReactionSummary> RemoveReactionAsync(Guid postId, Guid commentId, Guid userId, CancellationToken cancellationToken);
}
