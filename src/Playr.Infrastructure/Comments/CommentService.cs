using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Playr.Application.Badges;
using Playr.Application.Comments;
using Playr.Application.Common;
using Playr.Application.Notifications;
using Playr.Domain.Badges;
using Playr.Domain.Comments;
using Playr.Domain.Notifications;
using Playr.Domain.Posts;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Comments;

public sealed class CommentService(
    PlayrDbContext dbContext,
    INotificationFeedService notificationFeedService,
    IBadgeService badgeService,
    ILogger<CommentService> logger) : ICommentService
{
    private const int MaxTextLength = 500;

    public async Task<CommentDto> CreateAsync(Guid postId, Guid authorId, CreateCommentCommand command, CancellationToken cancellationToken)
    {
        var text = command.TextContent?.Trim() ?? string.Empty;
        if (text.Length == 0)
            throw new InvalidOperationException("Comment text is required.");
        if (text.Length > MaxTextLength)
            throw new InvalidOperationException($"Comment text cannot be longer than {MaxTextLength} characters.");

        var post = await dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancellationToken)
            ?? throw new InvalidOperationException("Post was not found.");

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

        if (command.MentionedUserIds is { Count: > 0 })
        {
            var candidateProfiles = await dbContext.UserProfiles
                .AsNoTracking()
                .Where(p => command.MentionedUserIds.Contains(p.UserId))
                .Select(p => new { p.UserId, p.Username })
                .ToListAsync(cancellationToken);
            var textVerifiedIds = MentionTextValidator.FilterPresentInText(
                text, candidateProfiles.Select(p => (p.UserId, p.Username)));

            var validMentionedIds = await notificationFeedService.CreateMentionNotificationsAsync(
                authorId, textVerifiedIds, NotificationType.CommentMention, postId, comment.Id, cancellationToken);

            if (validMentionedIds.Count > 0)
            {
                var mentionedProfiles = await dbContext.UserProfiles
                    .AsNoTracking()
                    .Where(p => validMentionedIds.Contains(p.UserId))
                    .ToListAsync(cancellationToken);
                dbContext.CommentMentions.AddRange(mentionedProfiles.Select(p => new CommentMention
                {
                    Id = Guid.NewGuid(),
                    CommentId = comment.Id,
                    MentionedUserId = p.UserId,
                    UsernameAtTimeOfPosting = p.Username,
                }));
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        var dtos = await MapToCommentDtoAsync([comment], authorId, cancellationToken);

        if (post.AuthorId != authorId)
        {
            try
            {
                await badgeService.CheckAndUnlockBadgesAsync(authorId, BadgeType.Commentator, cancellationToken);
            }
            catch (Exception ex)
            {
                // Best-effort side effect: badge-unlock failures must not fail comment creation.
                logger.LogError(ex, "Failed to evaluate Commentator badge for user {UserId}.", authorId);
            }
        }

        return dtos[0];
    }

    public async Task<PagedResult<CommentDto>> GetPagedAsync(Guid postId, Guid? currentUserId, int skip, int take, CancellationToken cancellationToken)
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

        var dtos = await MapToCommentDtoAsync(comments, currentUserId, cancellationToken);
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

        var dtos = await MapToCommentDtoAsync([comment], requesterId, cancellationToken);
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

    public async Task<CommentReactionSummary> SetReactionAsync(Guid postId, Guid commentId, Guid userId, ReactionType type, CancellationToken cancellationToken)
    {
        var comment = await dbContext.PostComments.FirstOrDefaultAsync(c => c.Id == commentId && c.PostId == postId, cancellationToken)
            ?? throw new InvalidOperationException("Comment was not found.");

        var existing = await dbContext.CommentReactions
            .FirstOrDefaultAsync(r => r.CommentId == comment.Id && r.UserId == userId, cancellationToken);

        var isNewReaction = existing is null;
        if (existing is null)
        {
            dbContext.CommentReactions.Add(new CommentReaction
            {
                CommentId = comment.Id,
                UserId = userId,
                Type = type,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
        else if (existing.Type == type)
        {
            dbContext.CommentReactions.Remove(existing);
        }
        else
        {
            existing.Type = type;
            existing.CreatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (isNewReaction)
        {
            try
            {
                await badgeService.CheckAndUnlockBadgesAsync(userId, BadgeType.Reactor, cancellationToken);
            }
            catch (Exception ex)
            {
                // Best-effort side effect: badge-unlock failures must not fail reacting to a comment.
                logger.LogError(ex, "Failed to evaluate Reactor badge for user {UserId}.", userId);
            }
        }

        return await BuildReactionSummaryAsync(comment.Id, userId, cancellationToken);
    }

    public async Task<CommentReactionSummary> RemoveReactionAsync(Guid postId, Guid commentId, Guid userId, CancellationToken cancellationToken)
    {
        var comment = await dbContext.PostComments.FirstOrDefaultAsync(c => c.Id == commentId && c.PostId == postId, cancellationToken)
            ?? throw new InvalidOperationException("Comment was not found.");

        var existing = await dbContext.CommentReactions
            .FirstOrDefaultAsync(r => r.CommentId == comment.Id && r.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            dbContext.CommentReactions.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await BuildReactionSummaryAsync(comment.Id, userId, cancellationToken);
    }

    private async Task<CommentReactionSummary> BuildReactionSummaryAsync(Guid commentId, Guid? currentUserId, CancellationToken cancellationToken)
    {
        var reactions = await dbContext.CommentReactions
            .AsNoTracking()
            .Where(r => r.CommentId == commentId)
            .ToListAsync(cancellationToken);

        var counts = BuildCounts(reactions);
        ReactionType? currentUserReaction = currentUserId.HasValue
            ? reactions.FirstOrDefault(r => r.UserId == currentUserId.Value)?.Type
            : null;

        return new CommentReactionSummary(counts, currentUserReaction);
    }

    private static ReactionCounts BuildCounts(IReadOnlyCollection<CommentReaction> reactions) => new(
        reactions.Count(r => r.Type == ReactionType.Like),
        reactions.Count(r => r.Type == ReactionType.Haha),
        reactions.Count(r => r.Type == ReactionType.Wow),
        reactions.Count(r => r.Type == ReactionType.Sad),
        reactions.Count(r => r.Type == ReactionType.Angry));

    private async Task<IReadOnlyList<CommentDto>> MapToCommentDtoAsync(IList<PostComment> comments, Guid? currentUserId, CancellationToken cancellationToken)
    {
        var authorIds = comments.Select(c => c.AuthorId).Distinct().ToList();
        var profiles = await dbContext.UserProfiles
            .AsNoTracking()
            .Where(up => authorIds.Contains(up.UserId))
            .ToListAsync(cancellationToken);
        var profileMap = profiles.ToDictionary(up => up.UserId);

        var commentIds = comments.Select(c => c.Id).ToList();
        var allReactions = await dbContext.CommentReactions
            .AsNoTracking()
            .Where(r => commentIds.Contains(r.CommentId))
            .ToListAsync(cancellationToken);
        var reactionsByComment = allReactions
            .GroupBy(r => r.CommentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var mentions = await dbContext.CommentMentions
            .AsNoTracking()
            .Where(m => commentIds.Contains(m.CommentId))
            .ToListAsync(cancellationToken);
        var mentionedUserIds = mentions.Select(m => m.MentionedUserId).Distinct().ToList();
        var mentionedProfileMap = (await dbContext.UserProfiles
            .AsNoTracking()
            .Where(p => mentionedUserIds.Contains(p.UserId))
            .ToListAsync(cancellationToken))
            .ToDictionary(p => p.UserId);
        var mentionsByComment = mentions
            .GroupBy(m => m.CommentId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<MentionDto>)g
                .Select(m => new MentionDto(
                    m.MentionedUserId,
                    m.UsernameAtTimeOfPosting,
                    mentionedProfileMap.TryGetValue(m.MentionedUserId, out var p) ? p.DisplayName : m.UsernameAtTimeOfPosting))
                .ToList());

        return comments.Select(comment =>
        {
            var profile = profileMap[comment.AuthorId];
            var commentReactions = reactionsByComment.TryGetValue(comment.Id, out var list) ? list : [];
            var counts = BuildCounts(commentReactions);
            ReactionType? currentUserReaction = currentUserId.HasValue
                ? commentReactions.FirstOrDefault(r => r.UserId == currentUserId.Value)?.Type
                : null;

            return new CommentDto(
                comment.Id,
                comment.PostId,
                comment.AuthorId,
                profile.Username,
                profile.DisplayName,
                profile.AvatarUrl,
                profile.ActiveBadgeType?.ToString(),
                profile.ActiveBadgeLevel?.ToString(),
                comment.TextContent,
                comment.CreatedAt,
                comment.UpdatedAt,
                new CommentReactionSummary(counts, currentUserReaction),
                mentionsByComment.GetValueOrDefault(comment.Id, []));
        }).ToList();
    }
}
