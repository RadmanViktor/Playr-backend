using Microsoft.EntityFrameworkCore;
using Playr.Application.Posts;
using Playr.Domain.Posts;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Posts;

public sealed class PostService(PlayrDbContext dbContext) : IPostService
{
    private const int MaxTextLength = 1000;
    private const int FeedSize = 50;

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

        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            GameId = command.GameId,
            TextContent = text,
            Mood = mood,
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

        var post = await dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancellationToken)
            ?? throw new InvalidOperationException("Post was not found.");

        if (post.AuthorId != requesterId)
            throw new InvalidOperationException("You are not allowed to edit this post.");

        post.TextContent = text;
        post.Mood = mood;
        await dbContext.SaveChangesAsync(cancellationToken);

        var dtos = await MapToPostDtoAsync([post], requesterId, cancellationToken);
        return dtos[0];
    }

    public async Task DeleteAsync(Guid postId, Guid requesterId, CancellationToken cancellationToken)
    {
        var post = await dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancellationToken)
            ?? throw new InvalidOperationException("Post was not found.");

        if (post.AuthorId != requesterId)
            throw new InvalidOperationException("You are not allowed to delete this post.");

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
                post.CreatedAt,
                likeCountMap.GetValueOrDefault(post.Id, 0),
                likedByCurrentUser.Contains(post.Id));
        }).ToList();
    }
}
