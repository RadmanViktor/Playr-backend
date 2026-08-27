using Microsoft.EntityFrameworkCore;
using Playr.Application.Posts;
using Playr.Application.Storage;
using Playr.Domain.Posts;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Posts;

public sealed class PostService(PlayrDbContext dbContext, IFileStorageService fileStorageService) : IPostService
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
            Media = mediaEntities,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dtos = await MapToPostDtoAsync([post], authorId, cancellationToken);
        return dtos[0];
    }

    public async Task<IReadOnlyList<PostDto>> GetFeedAsync(Guid? currentUserId, CancellationToken cancellationToken)
    {
        var feed = await dbContext.Posts
            .AsNoTracking()
            .Include(p => p.Media)
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
                post.Media.Add(media);
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
            .Where(p => p.AuthorId == profile.UserId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(FeedSize)
            .ToListAsync(cancellationToken);

        return await MapToPostDtoAsync(posts, currentUserId, cancellationToken);
    }

    public async Task<(int LikesCount, bool Liked)> ToggleLikeAsync(Guid postId, Guid userId, CancellationToken cancellationToken)
    {
        var postExists = await dbContext.Posts.AnyAsync(p => p.Id == postId, cancellationToken);
        if (!postExists)
            throw new InvalidOperationException("Post was not found.");

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
                game.Id,
                game.Name,
                game.CoverImageUrl,
                post.TextContent,
                post.Mood?.ToString(),
                post.Media
                    .OrderBy(m => m.SortOrder)
                    .Select(m => new PostMediaDto(m.Id, m.Url, m.MediaType.ToString(), m.SortOrder))
                    .ToList(),
                post.CreatedAt,
                likeCountMap.GetValueOrDefault(post.Id, 0),
                likedByCurrentUser.Contains(post.Id),
                commentCountMap.GetValueOrDefault(post.Id, 0));
        }).ToList();
    }
}
