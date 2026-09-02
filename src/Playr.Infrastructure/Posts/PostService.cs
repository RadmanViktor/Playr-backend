using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Playr.Application.Badges;
using Playr.Application.Common;
using Playr.Application.Notifications;
using Playr.Application.Posts;
using Playr.Application.Storage;
using Playr.Domain.Badges;
using Playr.Domain.Notifications;
using Playr.Domain.Posts;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Posts;

public sealed class PostService(
    PlayrDbContext dbContext,
    IFileStorageService fileStorageService,
    INotificationFeedService notificationFeedService,
    IBadgeService badgeService,
    ILogger<PostService> logger) : IPostService
{
    private const int MaxTextLength = 1000;
    private const int FeedSize = 50;
    private const string MediaSubFolder = "posts";

    public async Task<PostDto> CreateAsync(Guid authorId, CreatePostCommand command, CancellationToken cancellationToken)
    {
        var text = command.TextContent?.Trim() ?? string.Empty;
        if (text.Length == 0)
            throw new InvalidOperationException("Post text is required.");
        if (text.Length > MaxTextLength)
            throw new InvalidOperationException($"Post text cannot be longer than {MaxTextLength} characters.");

        PostMood? mood = null;
        if (command.Mood is not null)
        {
            if (!Enum.TryParse<PostMood>(command.Mood, ignoreCase: true, out var parsed))
                throw new InvalidOperationException("Invalid mood value.");
            mood = parsed;
        }

        var gameExists = await dbContext.Games.AnyAsync(g => g.Id == command.GameId, cancellationToken);
        if (!gameExists)
            throw new InvalidOperationException("Game was not found.");

        var scope = PostScope.Feed;
        if (command.Scope is not null)
        {
            if (!Enum.TryParse<PostScope>(command.Scope, ignoreCase: true, out var parsedScope))
                throw new InvalidOperationException("Invalid scope value.");
            scope = parsedScope;
        }

        var validatedMedia = PostMediaValidator.ValidateMany(command.Media);
        var mediaEntities = new List<PostMedia>();
        var sortOrder = 0;
        foreach (var (input, mediaType, extension) in validatedMedia)
        {
            var saved = await fileStorageService.SaveAsync(input.Content, extension, MediaSubFolder, cancellationToken);
            mediaEntities.Add(new PostMedia
            {
                Id = Guid.NewGuid(),
                Url = saved.RelativeUrl,
                MediaType = mediaType,
                SortOrder = sortOrder++,
            });
        }

        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            GameId = command.GameId,
            TextContent = text,
            Mood = mood,
            Scope = scope,
            Media = mediaEntities,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync(cancellationToken);

        IReadOnlyCollection<Guid> mentionedRecipientIds = [];
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
                authorId, textVerifiedIds, NotificationType.PostMention, post.Id, null, cancellationToken);
            mentionedRecipientIds = validMentionedIds;

            if (validMentionedIds.Count > 0)
            {
                var mentionedProfiles = await dbContext.UserProfiles
                    .AsNoTracking()
                    .Where(p => validMentionedIds.Contains(p.UserId))
                    .ToListAsync(cancellationToken);
                dbContext.PostMentions.AddRange(mentionedProfiles.Select(p => new PostMention
                {
                    Id = Guid.NewGuid(),
                    PostId = post.Id,
                    MentionedUserId = p.UserId,
                    UsernameAtTimeOfPosting = p.Username,
                }));
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        await notificationFeedService.CreateFollowerPostNotificationsAsync(
            authorId, post.Id, mentionedRecipientIds, cancellationToken);

        var dtos = await MapToPostDtoAsync([post], authorId, cancellationToken);

        try
        {
            await badgeService.CheckAndUnlockBadgesAsync(authorId, BadgeType.Poster, cancellationToken);
        }
        catch (Exception ex)
        {
            // Best-effort side effect: badge-unlock failures must not fail post creation.
            logger.LogError(ex, "Failed to evaluate Poster badge for user {UserId}.", authorId);
        }

        try
        {
            var postCount = await dbContext.Posts.CountAsync(p => p.AuthorId == authorId, cancellationToken);
            if (postCount == 1)
            {
                await badgeService.GrantBadgeAsync(authorId, BadgeType.Trailblazer, BadgeLevel.Gold, cancellationToken);
            }

            var hourUtc = post.CreatedAt.UtcDateTime.Hour;
            if (hourUtc is >= 0 and < 5)
            {
                await badgeService.GrantBadgeAsync(authorId, BadgeType.NightOwl, BadgeLevel.Gold, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Best-effort side effect: badge-unlock failures must not fail post creation.
            logger.LogError(ex, "Failed to evaluate Trailblazer/NightOwl badge for user {UserId}.", authorId);
        }

        return dtos[0];
    }

    public async Task<PostDto?> GetByIdAsync(Guid postId, Guid? currentUserId, CancellationToken cancellationToken)
    {
        var post = await dbContext.Posts
            .AsNoTracking()
            .Include(p => p.Media)
            .FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);

        if (post is null)
        {
            return null;
        }

        var dtos = await MapToPostDtoAsync([post], currentUserId, cancellationToken);
        return dtos[0];
    }

    public async Task<IReadOnlyList<PostDto>> GetFeedAsync(Guid? currentUserId, CancellationToken cancellationToken)
    {
        var feed = await dbContext.Posts
            .AsNoTracking()
            .Include(p => p.Media)
            .Where(p => p.Scope == PostScope.Feed)
            .OrderByDescending(p => p.CreatedAt)
            .Take(FeedSize)
            .ToListAsync(cancellationToken);

        return await MapToPostDtoAsync(feed, currentUserId, cancellationToken);
    }

    public async Task<PostDto> UpdateAsync(Guid postId, Guid requesterId, UpdatePostCommand command, CancellationToken cancellationToken)
    {
        var text = command.TextContent?.Trim() ?? string.Empty;
        if (text.Length == 0)
            throw new InvalidOperationException("Post text is required.");
        if (text.Length > MaxTextLength)
            throw new InvalidOperationException($"Post text cannot be longer than {MaxTextLength} characters.");

        PostMood? mood = null;
        if (command.Mood is not null)
        {
            if (!Enum.TryParse<PostMood>(command.Mood, ignoreCase: true, out var parsed))
                throw new InvalidOperationException("Invalid mood value.");
            mood = parsed;
        }

        var post = await dbContext.Posts.Include(p => p.Media).FirstOrDefaultAsync(p => p.Id == postId, cancellationToken)
            ?? throw new InvalidOperationException("Post was not found.");

        if (post.AuthorId != requesterId)
            throw new InvalidOperationException("You are not allowed to edit this post.");

        if (command.RemoveMediaIds.Count > 0)
        {
            var toRemove = post.Media.Where(m => command.RemoveMediaIds.Contains(m.Id)).ToList();
            foreach (var media in toRemove)
            {
                fileStorageService.Delete(media.Url);
                post.Media.Remove(media);
                dbContext.PostMedia.Remove(media);
            }
        }

        if (command.NewMedia.Count > 0)
        {
            var remainingSlots = PostMediaValidator.MaxImageCount - post.Media.Count;
            if (post.Media.Any(m => m.MediaType == PostMediaType.Video) || command.NewMedia.Count > remainingSlots)
                throw new InvalidOperationException("A post can only contain a single video, or up to 5 images, not both.");

            var validatedMedia = PostMediaValidator.ValidateMany(command.NewMedia);
            var hasExistingVideo = post.Media.Count > 0 && validatedMedia.Any(v => v.MediaType == PostMediaType.Video);
            if (hasExistingVideo)
                throw new InvalidOperationException("A post can only contain a single video, or up to 5 images, not both.");

            var sortOrder = post.Media.Count == 0 ? 0 : post.Media.Max(m => m.SortOrder) + 1;
            foreach (var (input, mediaType, extension) in validatedMedia)
            {
                var saved = await fileStorageService.SaveAsync(input.Content, extension, MediaSubFolder, cancellationToken);
                var media = new PostMedia
                {
                    Id = Guid.NewGuid(),
                    PostId = post.Id,
                    Url = saved.RelativeUrl,
                    MediaType = mediaType,
                    SortOrder = sortOrder++,
                };
                dbContext.PostMedia.Add(media);
            }
        }

        post.TextContent = text;
        post.Mood = mood;
        await dbContext.SaveChangesAsync(cancellationToken);

        var dtos = await MapToPostDtoAsync([post], requesterId, cancellationToken);
        return dtos[0];
    }

    public async Task DeleteAsync(Guid postId, Guid requesterId, CancellationToken cancellationToken)
    {
        var post = await dbContext.Posts.Include(p => p.Media).FirstOrDefaultAsync(p => p.Id == postId, cancellationToken)
            ?? throw new InvalidOperationException("Post was not found.");

        if (post.AuthorId != requesterId)
            throw new InvalidOperationException("You are not allowed to delete this post.");

        foreach (var media in post.Media)
            fileStorageService.Delete(media.Url);

        dbContext.Posts.Remove(post);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PostDto>> GetByUsernameAsync(string username, Guid? currentUserId, CancellationToken cancellationToken)
    {
        var normalized = username.ToUpperInvariant();
        var profile = await dbContext.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Username.ToUpper() == normalized, cancellationToken);

        if (profile is null)
            return [];

        var posts = await dbContext.Posts
            .AsNoTracking()
            .Include(p => p.Media)
            .Where(p => p.AuthorId == profile.UserId && p.Scope == PostScope.Profile)
            .OrderByDescending(p => p.CreatedAt)
            .Take(FeedSize)
            .ToListAsync(cancellationToken);

        return await MapToPostDtoAsync(posts, currentUserId, cancellationToken);
    }

    public async Task<(int LikesCount, bool Liked)> ToggleLikeAsync(Guid postId, Guid userId, CancellationToken cancellationToken)
    {
        var post = await dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancellationToken)
            ?? throw new InvalidOperationException("Post was not found.");

        var existingLike = await dbContext.PostLikes
            .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId, cancellationToken);

        bool liked;
        if (existingLike is null)
        {
            dbContext.PostLikes.Add(new PostLike { PostId = postId, UserId = userId, CreatedAt = DateTimeOffset.UtcNow });
            liked = true;
        }
        else
        {
            dbContext.PostLikes.Remove(existingLike);
            liked = false;
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        if (liked)
        {
            await notificationFeedService.CreatePostEngagementNotificationAsync(
                userId,
                post.AuthorId,
                NotificationType.PostLiked,
                postId,
                null,
                cancellationToken);

            try
            {
                await badgeService.CheckAndUnlockBadgesAsync(userId, BadgeType.Supporter, cancellationToken);
                await badgeService.CheckAndUnlockBadgesAsync(post.AuthorId, BadgeType.Popular, cancellationToken);
            }
            catch (Exception ex)
            {
                // Best-effort side effect: badge-unlock failures must not fail liking a post.
                logger.LogError(ex, "Failed to evaluate Supporter/Popular badge for post {PostId}.", postId);
            }
        }

        var likesCount = await dbContext.PostLikes.CountAsync(l => l.PostId == postId, cancellationToken);
        return (likesCount, liked);
    }

    private async Task<IReadOnlyList<PostDto>> MapToPostDtoAsync(IList<Post> posts, Guid? currentUserId, CancellationToken cancellationToken)
    {
        var authorIds = posts.Select(p => p.AuthorId).Distinct().ToList();
        var profiles = await dbContext.UserProfiles
            .AsNoTracking()
            .Where(up => authorIds.Contains(up.UserId))
            .ToListAsync(cancellationToken);

        var profileMap = profiles.ToDictionary(up => up.UserId);

        var gameIds = posts.Select(p => p.GameId).Distinct().ToList();
        var games = await dbContext.Games
            .AsNoTracking()
            .Where(g => gameIds.Contains(g.Id))
            .ToListAsync(cancellationToken);
        var gameMap = games.ToDictionary(g => g.Id);

        var postIds = posts.Select(p => p.Id).ToList();
        var likeCounts = await dbContext.PostLikes
            .AsNoTracking()
            .Where(l => postIds.Contains(l.PostId))
            .GroupBy(l => l.PostId)
            .Select(g => new { PostId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var likeCountMap = likeCounts.ToDictionary(x => x.PostId, x => x.Count);

        var likedByCurrentUser = new HashSet<Guid>();
        if (currentUserId is Guid uid)
        {
            var liked = await dbContext.PostLikes
                .AsNoTracking()
                .Where(l => postIds.Contains(l.PostId) && l.UserId == uid)
                .Select(l => l.PostId)
                .ToListAsync(cancellationToken);
            likedByCurrentUser = liked.ToHashSet();
        }

        var commentCounts = await dbContext.PostComments
            .AsNoTracking()
            .Where(c => postIds.Contains(c.PostId))
            .GroupBy(c => c.PostId)
            .Select(g => new { PostId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var commentCountMap = commentCounts.ToDictionary(x => x.PostId, x => x.Count);

        var mentions = await dbContext.PostMentions
            .AsNoTracking()
            .Where(m => postIds.Contains(m.PostId))
            .ToListAsync(cancellationToken);
        var mentionedUserIds = mentions.Select(m => m.MentionedUserId).Distinct().ToList();
        var mentionedProfileMap = (await dbContext.UserProfiles
            .AsNoTracking()
            .Where(p => mentionedUserIds.Contains(p.UserId))
            .ToListAsync(cancellationToken))
            .ToDictionary(p => p.UserId);
        var mentionsByPost = mentions
            .GroupBy(m => m.PostId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<MentionDto>)g
                .Select(m => new MentionDto(
                    m.MentionedUserId,
                    m.UsernameAtTimeOfPosting,
                    mentionedProfileMap.TryGetValue(m.MentionedUserId, out var p) ? p.DisplayName : m.UsernameAtTimeOfPosting))
                .ToList());

        return posts.Select(post =>
        {
            var profile = profileMap[post.AuthorId];
            var game = gameMap[post.GameId];
            return new PostDto(
                post.Id,
                post.AuthorId,
                profile.Username,
                profile.DisplayName,
                profile.AvatarUrl,
                profile.ActiveBadgeType?.ToString(),
                profile.ActiveBadgeLevel?.ToString(),
                game.Id,
                game.Name,
                game.CoverImageUrl,
                post.TextContent,
                post.Mood?.ToString(),
                post.Scope.ToString(),
                post.Media
                    .OrderBy(m => m.SortOrder)
                    .Select(m => new PostMediaDto(m.Id, m.Url, m.MediaType.ToString(), m.SortOrder))
                    .ToList(),
                post.CreatedAt,
                likeCountMap.GetValueOrDefault(post.Id, 0),
                likedByCurrentUser.Contains(post.Id),
                commentCountMap.GetValueOrDefault(post.Id, 0),
                mentionsByPost.GetValueOrDefault(post.Id, []));
        }).ToList();
    }
}
