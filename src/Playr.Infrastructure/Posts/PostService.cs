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

        var dtos = await MapToPostDtoAsync([post], cancellationToken);
        return dtos[0];
    }

    public async Task<IReadOnlyList<PostDto>> GetFeedAsync(CancellationToken cancellationToken)
    {
        var feed = await dbContext.Posts
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Take(FeedSize)
            .ToListAsync(cancellationToken);

        return await MapToPostDtoAsync(feed, cancellationToken);
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

        var dtos = await MapToPostDtoAsync([post], cancellationToken);
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

    private async Task<IReadOnlyList<PostDto>> MapToPostDtoAsync(IList<Post> posts, CancellationToken cancellationToken)
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
                post.CreatedAt);
        }).ToList();
    }
}
