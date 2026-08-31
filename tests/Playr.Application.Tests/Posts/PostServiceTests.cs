using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Posts;
using Playr.Domain.Games;
using Playr.Domain.Identity;
using Playr.Domain.Posts;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Posts;

namespace Playr.Application.Tests.Posts;

public sealed class PostServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PlayrDbContext _dbContext;
    private readonly PostService _service;
    private readonly Guid _authorId;
    private readonly Guid _gameId;

    public PostServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<PlayrDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new PlayrDbContext(options);
        _dbContext.Database.EnsureCreated();

        _authorId = Guid.NewGuid();
        _gameId = Guid.NewGuid();

        _dbContext.Users.Add(new ApplicationUser
        {
            Id = _authorId,
            Email = "player@example.com",
            UserName = "player",
            NormalizedEmail = "PLAYER@EXAMPLE.COM",
            NormalizedUserName = "PLAYER",
        });
        _dbContext.UserProfiles.Add(new UserProfile
        {
            UserId = _authorId,
            Username = "player",
            DisplayName = "Player One",
        });
        _dbContext.Games.Add(new Game { Id = _gameId, Name = "Hollow Knight" });
        _dbContext.SaveChanges();

        _service = new PostService(_dbContext, new NoOpFileStorageService(), new Playr.Application.Tests.Notifications.NoOpNotificationFeedService(), new Playr.Application.Tests.Badges.NoOpBadgeService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<PostService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_with_valid_data_returns_post_dto()
    {
        var command = new CreatePostCommand(_gameId, "Cleared the boss!", null, null);
        var result = await _service.CreateAsync(_authorId, command, CancellationToken.None);

        result.Id.Should().NotBeEmpty();
        result.AuthorId.Should().Be(_authorId);
        result.AuthorUsername.Should().Be("player");
        result.AuthorDisplayName.Should().Be("Player One");
        result.GameId.Should().Be(_gameId);
        result.GameName.Should().Be("Hollow Knight");
        result.TextContent.Should().Be("Cleared the boss!");
        result.Mood.Should().BeNull();
        result.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateAsync_with_mood_sets_mood_string()
    {
        var command = new CreatePostCommand(_gameId, "So fun!", "Enjoying", null);
        var result = await _service.CreateAsync(_authorId, command, CancellationToken.None);

        result.Mood.Should().Be("Enjoying");
    }

    [Fact]
    public async Task CreateAsync_with_null_mood_sets_mood_null()
    {
        var command = new CreatePostCommand(_gameId, "Just playing.", null, null);
        var result = await _service.CreateAsync(_authorId, command, CancellationToken.None);

        result.Mood.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_with_empty_text_throws()
    {
        var command = new CreatePostCommand(_gameId, "   ", null, null);
        var act = () => _service.CreateAsync(_authorId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Post text is required.");
    }

    [Fact]
    public async Task CreateAsync_with_text_too_long_throws()
    {
        var command = new CreatePostCommand(_gameId, new string('a', 1001), null, null);
        var act = () => _service.CreateAsync(_authorId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Post text cannot be longer than 1000 characters.");
    }

    [Fact]
    public async Task CreateAsync_with_invalid_mood_throws()
    {
        var command = new CreatePostCommand(_gameId, "Hello!", "Raging", null);
        var act = () => _service.CreateAsync(_authorId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Invalid mood value.");
    }

    [Fact]
    public async Task CreateAsync_with_unknown_game_throws()
    {
        var command = new CreatePostCommand(Guid.NewGuid(), "Hello!", null, null);
        var act = () => _service.CreateAsync(_authorId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Game was not found.");
    }

    [Fact]
    public async Task GetFeedAsync_returns_posts_newest_first()
    {
        var older = new CreatePostCommand(_gameId, "First post", null, null);
        var newer = new CreatePostCommand(_gameId, "Second post", null, null);
        await _service.CreateAsync(_authorId, older, CancellationToken.None);
        await Task.Delay(10);
        await _service.CreateAsync(_authorId, newer, CancellationToken.None);

        var feed = await _service.GetFeedAsync(null, CancellationToken.None);

        feed.Should().HaveCount(2);
        feed[0].TextContent.Should().Be("Second post");
        feed[1].TextContent.Should().Be("First post");
    }

    [Fact]
    public async Task GetFeedAsync_returns_at_most_50_posts()
    {
        for (var i = 0; i < 55; i++)
        {
            _dbContext.Posts.Add(new Post
            {
                Id = Guid.NewGuid(),
                AuthorId = _authorId,
                GameId = _gameId,
                TextContent = $"Post {i}",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i),
            });
        }
        await _dbContext.SaveChangesAsync();

        var feed = await _service.GetFeedAsync(null, CancellationToken.None);

        feed.Should().HaveCount(50);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
