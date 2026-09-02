using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Comments;
using Playr.Application.Tests.Notifications;
using Playr.Domain.Friendships;
using Playr.Domain.Games;
using Playr.Domain.Identity;
using Playr.Domain.Posts;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Comments;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Notifications;

namespace Playr.Application.Tests.Comments;

public sealed class CommentMentionsTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PlayrDbContext _dbContext;
    private readonly CommentService _service;
    private readonly SpyNotificationNotifier _notifier;
    private readonly Guid _authorId;
    private readonly Guid _friendId;
    private readonly Guid _postId;

    public CommentMentionsTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<PlayrDbContext>().UseSqlite(_connection).Options;
        _dbContext = new PlayrDbContext(options);
        _dbContext.Database.EnsureCreated();

        _authorId = Guid.NewGuid();
        _friendId = Guid.NewGuid();
        _postId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        AddUser(_authorId, "author", "Author");
        AddUser(_friendId, "friend", "Friend Name");
        _dbContext.Friendships.Add(new Friendship { Id = Guid.NewGuid(), UserAId = _authorId, UserBId = _friendId, CreatedAt = DateTimeOffset.UtcNow });
        _dbContext.Games.Add(new Game { Id = gameId, Name = "Hollow Knight" });
        _dbContext.Posts.Add(new Post { Id = _postId, AuthorId = _authorId, GameId = gameId, TextContent = "Post", CreatedAt = DateTimeOffset.UtcNow });
        _dbContext.SaveChanges();

        _notifier = new SpyNotificationNotifier();
        _service = new CommentService(_dbContext, new NotificationFeedService(_dbContext, _notifier), new Playr.Application.Tests.Badges.NoOpBadgeService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<CommentService>.Instance);
    }

    private void AddUser(Guid id, string username, string displayName)
    {
        _dbContext.Users.Add(new ApplicationUser
        {
            Id = id,
            Email = $"{username}@example.com",
            UserName = username,
            NormalizedEmail = $"{username.ToUpperInvariant()}@EXAMPLE.COM",
            NormalizedUserName = username.ToUpperInvariant(),
        });
        _dbContext.UserProfiles.Add(new UserProfile { UserId = id, Username = username, DisplayName = displayName });
    }

    [Fact]
    public async Task CreateAsync_with_friend_mention_returns_mention_in_dto_and_creates_notification()
    {
        var command = new CreateCommentCommand("Nice one @friend", [_friendId]);
        var comment = await _service.CreateAsync(_postId, _authorId, command, CancellationToken.None);

        comment.Mentions.Should().ContainSingle();
        comment.Mentions[0].UserId.Should().Be(_friendId);
        comment.Mentions[0].Username.Should().Be("friend");

        _notifier.Notified.Should().ContainSingle(n =>
            n.RecipientUserId == _friendId && n.Type == "CommentMention" && n.CommentId == comment.Id && n.PostId == _postId);
    }

    [Fact]
    public async Task CreateAsync_when_another_user_comments_notifies_post_owner()
    {
        var comment = await _service.CreateAsync(
            _postId,
            _friendId,
            new CreateCommentCommand("Nice post", null),
            CancellationToken.None);

        _notifier.Notified.Should().ContainSingle(n =>
            n.RecipientUserId == _authorId &&
            n.Actor.UserId == _friendId &&
            n.Type == "PostCommented" &&
            n.PostId == _postId &&
            n.CommentId == comment.Id);
    }

    [Fact]
    public async Task CreateAsync_when_owner_comments_own_post_does_not_notify()
    {
        await _service.CreateAsync(
            _postId,
            _authorId,
            new CreateCommentCommand("Adding context", null),
            CancellationToken.None);

        _notifier.Notified.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_when_comment_mentions_owner_sends_only_mention_notification()
    {
        await _service.CreateAsync(
            _postId,
            _friendId,
            new CreateCommentCommand("Thanks @author", [_authorId]),
            CancellationToken.None);

        _notifier.Notified.Should().ContainSingle(n =>
            n.RecipientUserId == _authorId && n.Type == "CommentMention");
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
