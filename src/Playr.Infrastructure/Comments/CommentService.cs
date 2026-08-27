using Microsoft.EntityFrameworkCore;
using Playr.Application.Comments;
using Playr.Application.Common;
using Playr.Domain.Posts;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Comments;

public sealed class CommentService(PlayrDbContext dbContext) : ICommentService
{
    private const int MaxTextLength = 500;

    public async Task<CommentDto> CreateAsync(Guid postId, Guid authorId, CreateCommentCommand command, CancellationToken cancellationToken)
    {
        var text = command.TextContent?.Trim() ?? string.Empty;
        if (text.Length == 0)
            throw new InvalidOperationException("Comment text is required.");
        if (text.Length > MaxTextLength)
            throw new InvalidOperationException($"Comment text cannot be longer than {MaxTextLength} characters.");

        var postExists = await dbContext.Posts.AnyAsync(p => p.Id == postId, cancellationToken);
        if (!postExists)
            throw new InvalidOperationException("Post was not found.");

        var comment = new PostComment
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            AuthorId = authorId,
            TextContent = text,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.PostComments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dtos = await MapToCommentDtoAsync([comment], cancellationToken);
        return dtos[0];
    }

    public async Task<PagedResult<CommentDto>> GetPagedAsync(Guid postId, int skip, int take, CancellationToken cancellationToken)
    {
        var postExists = await dbContext.Posts.AnyAsync(p => p.Id == postId, cancellationToken);
        if (!postExists)
            throw new InvalidOperationException("Post was not found.");

        var totalCount = await dbContext.PostComments.CountAsync(c => c.PostId == postId, cancellationToken);

        var comments = await dbContext.PostComments
            .AsNoTracking()
            .Where(c => c.PostId == postId)
            .OrderBy(c => c.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var dtos = await MapToCommentDtoAsync(comments, cancellationToken);
        var hasMore = skip + comments.Count < totalCount;
        return new PagedResult<CommentDto>(dtos, totalCount, hasMore);
    }

    public async Task<CommentDto> UpdateAsync(Guid postId, Guid commentId, Guid requesterId, UpdateCommentCommand command, CancellationToken cancellationToken)
    {
        var text = command.TextContent?.Trim() ?? string.Empty;
        if (text.Length == 0)
            throw new InvalidOperationException("Comment text is required.");
        if (text.Length > MaxTextLength)
            throw new InvalidOperationException($"Comment text cannot be longer than {MaxTextLength} characters.");

        var comment = await dbContext.PostComments.FirstOrDefaultAsync(c => c.Id == commentId && c.PostId == postId, cancellationToken)
            ?? throw new InvalidOperationException("Comment was not found.");

        if (comment.AuthorId != requesterId)
            throw new InvalidOperationException("You are not allowed to edit this comment.");

        comment.TextContent = text;
        comment.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var dtos = await MapToCommentDtoAsync([comment], cancellationToken);
        return dtos[0];
    }

    public async Task DeleteAsync(Guid postId, Guid commentId, Guid requesterId, CancellationToken cancellationToken)
    {
        var comment = await dbContext.PostComments.FirstOrDefaultAsync(c => c.Id == commentId && c.PostId == postId, cancellationToken)
            ?? throw new InvalidOperationException("Comment was not found.");

        if (comment.AuthorId != requesterId)
            throw new InvalidOperationException("You are not allowed to delete this comment.");

        dbContext.PostComments.Remove(comment);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<CommentDto>> MapToCommentDtoAsync(IList<PostComment> comments, CancellationToken cancellationToken)
    {
        var authorIds = comments.Select(c => c.AuthorId).Distinct().ToList();
        var profiles = await dbContext.UserProfiles
            .AsNoTracking()
            .Where(up => authorIds.Contains(up.UserId))
            .ToListAsync(cancellationToken);
        var profileMap = profiles.ToDictionary(up => up.UserId);

        return comments.Select(comment =>
        {
            var profile = profileMap[comment.AuthorId];
            return new CommentDto(
                comment.Id,
                comment.PostId,
                comment.AuthorId,
                profile.Username,
                profile.DisplayName,
                profile.AvatarUrl,
                comment.TextContent,
                comment.CreatedAt,
                comment.UpdatedAt);
        }).ToList();
    }
}
