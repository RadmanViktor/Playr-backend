using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Comments;
using Playr.Domain.Comments;
using Playr.Domain.Games;
using Playr.Domain.Identity;
using Playr.Domain.Posts;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Comments;
using Playr.Infrastructure.Data;

namespace Playr.Application.Tests.Comments;

public sealed class CommentServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PlayrDbContext _dbContext;
    private readonly CommentService _service;
    private readonly Guid _authorId;
    private readonly Guid _reactorId;
    private readonly Guid _postId;
    private readonly Guid _commentId;

    public CommentServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<PlayrDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new PlayrDbContext(options);
        _dbContext.Database.EnsureCreated();

        _authorId = Guid.NewGuid();
        _reactorId = Guid.NewGuid();
        _postId = Guid.NewGuid();
        _commentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _dbContext.Users.AddRange(
            new ApplicationUser { Id = _authorId, Email = "author@example.com", UserName = "author", NormalizedEmail = "AUTHOR@EXAMPLE.COM", NormalizedUserName = "AUTHOR" },
            new ApplicationUser { Id = _reactorId, Email = "reactor@example.com", UserName = "reactor", NormalizedEmail = "REACTOR@EXAMPLE.COM", NormalizedUserName = "REACTOR" });
        _dbContext.UserProfiles.AddRange(
            new UserProfile { UserId = _authorId, Username = "author", DisplayName = "Author" },
            new UserProfile { UserId = _reactorId, Username = "reactor", DisplayName = "Reactor" });
        _dbContext.Games.Add(new Game { Id = gameId, Name = "Hollow Knight" });
        _dbContext.Posts.Add(new Post { Id = _postId, AuthorId = _authorId, GameId = gameId, TextContent = "Post", CreatedAt = DateTimeOffset.UtcNow });
        _dbContext.PostComments.Add(new PostComment { Id = _commentId, PostId = _postId, AuthorId = _authorId, TextContent = "Nice!", CreatedAt = DateTimeOffset.UtcNow });
        _dbContext.SaveChanges();

        _service = new CommentService(_dbContext, new Playr.Application.Tests.Notifications.NoOpNotificationFeedService(), new Playr.Application.Tests.Badges.NoOpBadgeService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<CommentService>.Instance);
    }

    [Fact]
    public async Task SetReactionAsync_with_no_existing_reaction_creates_it()
    {
        var summary = await _service.SetReactionAsync(_postId, _commentId, _reactorId, ReactionType.Like, CancellationToken.None);

        summary.Counts.Like.Should().Be(1);
        summary.Counts.Haha.Should().Be(0);
        summary.CurrentUserReaction.Should().Be(ReactionType.Like);
    }

    [Fact]
    public async Task SetReactionAsync_with_same_type_again_toggles_it_off()
    {
        await _service.SetReactionAsync(_postId, _commentId, _reactorId, ReactionType.Like, CancellationToken.None);
        var summary = await _service.SetReactionAsync(_postId, _commentId, _reactorId, ReactionType.Like, CancellationToken.None);

        summary.Counts.Like.Should().Be(0);
        summary.CurrentUserReaction.Should().BeNull();
    }

    [Fact]
    public async Task SetReactionAsync_with_different_type_replaces_existing_reaction()
    {
        await _service.SetReactionAsync(_postId, _commentId, _reactorId, ReactionType.Like, CancellationToken.None);
        var summary = await _service.SetReactionAsync(_postId, _commentId, _reactorId, ReactionType.Wow, CancellationToken.None);

        summary.Counts.Like.Should().Be(0);
        summary.Counts.Wow.Should().Be(1);
        summary.CurrentUserReaction.Should().Be(ReactionType.Wow);
    }

    [Fact]
    public async Task SetReactionAsync_with_unknown_comment_throws()
    {
        var act = () => _service.SetReactionAsync(_postId, Guid.NewGuid(), _reactorId, ReactionType.Like, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Comment was not found.");
    }

    [Fact]
    public async Task RemoveReactionAsync_removes_existing_reaction()
    {
        await _service.SetReactionAsync(_postId, _commentId, _reactorId, ReactionType.Angry, CancellationToken.None);
        var summary = await _service.RemoveReactionAsync(_postId, _commentId, _reactorId, CancellationToken.None);

        summary.Counts.Angry.Should().Be(0);
        summary.CurrentUserReaction.Should().BeNull();
    }

    [Fact]
    public async Task RemoveReactionAsync_with_no_existing_reaction_is_a_noop()
    {
        var summary = await _service.RemoveReactionAsync(_postId, _commentId, _reactorId, CancellationToken.None);

        summary.Counts.Like.Should().Be(0);
        summary.CurrentUserReaction.Should().BeNull();
    }

    [Fact]
    public async Task GetPagedAsync_reports_reaction_counts_and_current_user_reaction()
    {
        await _service.SetReactionAsync(_postId, _commentId, _reactorId, ReactionType.Haha, CancellationToken.None);
        await _service.SetReactionAsync(_postId, _commentId, _authorId, ReactionType.Haha, CancellationToken.None);

        var pageForReactor = await _service.GetPagedAsync(_postId, _reactorId, 0, 20, CancellationToken.None);
        var pageForAnonymous = await _service.GetPagedAsync(_postId, null, 0, 20, CancellationToken.None);

        pageForReactor.Items[0].Reactions.Counts.Haha.Should().Be(2);
        pageForReactor.Items[0].Reactions.CurrentUserReaction.Should().Be(ReactionType.Haha);
        pageForAnonymous.Items[0].Reactions.CurrentUserReaction.Should().BeNull();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
