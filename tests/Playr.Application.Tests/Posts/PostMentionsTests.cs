using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Posts;
using Playr.Application.Tests.Notifications;
using Playr.Domain.Friendships;
using Playr.Domain.Games;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Notifications;
using Playr.Infrastructure.Posts;

namespace Playr.Application.Tests.Posts;

public sealed class PostMentionsTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PlayrDbContext _dbContext;
    private readonly PostService _service;
    private readonly SpyNotificationNotifier _notifier;
    private readonly Guid _authorId;
    private readonly Guid _friendId;
    private readonly Guid _strangerId;
    private readonly Guid _gameId;

    public PostMentionsTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<PlayrDbContext>().UseSqlite(_connection).Options;
        _dbContext = new PlayrDbContext(options);
        _dbContext.Database.EnsureCreated();

        _authorId = Guid.NewGuid();
        _friendId = Guid.NewGuid();
        _strangerId = Guid.NewGuid();
        _gameId = Guid.NewGuid();

        AddUser(_authorId, "author", "Author");
        AddUser(_friendId, "friend", "Friend Name");
        AddUser(_strangerId, "stranger", "Stranger");
        _dbContext.Friendships.Add(new Friendship { Id = Guid.NewGuid(), UserAId = _authorId, UserBId = _friendId, CreatedAt = DateTimeOffset.UtcNow });
        _dbContext.Games.Add(new Game { Id = _gameId, Name = "Hollow Knight" });
        _dbContext.SaveChanges();

        _notifier = new SpyNotificationNotifier();
        _service = new PostService(_dbContext, new NoOpFileStorageService(), new NotificationFeedService(_dbContext, _notifier));
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
        var command = new CreatePostCommand(_gameId, "Great game @friend!", null, null, [_friendId]);
        var post = await _service.CreateAsync(_authorId, command, CancellationToken.None);

        post.Mentions.Should().ContainSingle();
        post.Mentions[0].UserId.Should().Be(_friendId);
        post.Mentions[0].Username.Should().Be("friend");
        post.Mentions[0].DisplayName.Should().Be("Friend Name");

        _notifier.Notified.Should().ContainSingle(n => n.RecipientUserId == _friendId && n.Type == "PostMention" && n.PostId == post.Id);
    }

    [Fact]
    public async Task CreateAsync_with_non_friend_mention_drops_it_silently()
    {
        var command = new CreatePostCommand(_gameId, "Hey @stranger", null, null, [_strangerId]);
        var post = await _service.CreateAsync(_authorId, command, CancellationToken.None);

        post.Mentions.Should().BeEmpty();
        _notifier.Notified.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_with_self_mention_drops_it_silently()
    {
        var command = new CreatePostCommand(_gameId, "Talking to myself", null, null, [_authorId]);
        var post = await _service.CreateAsync(_authorId, command, CancellationToken.None);

        post.Mentions.Should().BeEmpty();
        _notifier.Notified.Should().BeEmpty();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
