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

public sealed class PostEditDeleteServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PlayrDbContext _dbContext;
    private readonly PostService _service;
    private readonly Guid _authorId;
    private readonly Guid _otherId;
    private readonly Guid _gameId;
    private Guid _postId;

    public PostEditDeleteServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<PlayrDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new PlayrDbContext(options);
        _dbContext.Database.EnsureCreated();

        _authorId = Guid.NewGuid();
        _otherId = Guid.NewGuid();
        _gameId = Guid.NewGuid();

        _dbContext.Users.Add(new ApplicationUser
        {
            Id = _authorId, Email = "author@example.com", UserName = "author",
            NormalizedEmail = "AUTHOR@EXAMPLE.COM", NormalizedUserName = "AUTHOR",
        });
        _dbContext.UserProfiles.Add(new UserProfile
        {
            UserId = _authorId, Username = "author", DisplayName = "Author",
        });
        _dbContext.Users.Add(new ApplicationUser
        {
            Id = _otherId, Email = "other@example.com", UserName = "other",
            NormalizedEmail = "OTHER@EXAMPLE.COM", NormalizedUserName = "OTHER",
        });
        _dbContext.UserProfiles.Add(new UserProfile
        {
            UserId = _otherId, Username = "other", DisplayName = "Other",
        });
        _dbContext.Games.Add(new Game { Id = _gameId, Name = "Hollow Knight" });
        var post = new Post
        {
            Id = Guid.NewGuid(), AuthorId = _authorId, GameId = _gameId,
            TextContent = "Original text", Mood = PostMood.Enjoying,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _dbContext.Posts.Add(post);
        _dbContext.SaveChanges();
        _postId = post.Id;
        _service = new PostService(_dbContext);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesTextAndMood()
    {
        var command = new UpdatePostCommand("Updated text", "Completed");
        var result = await _service.UpdateAsync(_postId, _authorId, command, CancellationToken.None);

        result.TextContent.Should().Be("Updated text");
        result.Mood.Should().Be("Completed");
        result.AuthorUsername.Should().Be("author");
        result.GameName.Should().Be("Hollow Knight");
    }

    [Fact]
    public async Task UpdateAsync_WithNullMood_ClearsMood()
    {
        var command = new UpdatePostCommand("Some text", null);
        var result = await _service.UpdateAsync(_postId, _authorId, command, CancellationToken.None);

        result.Mood.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithEmptyText_Throws()
    {
        var command = new UpdatePostCommand("   ", null);
        var act = () => _service.UpdateAsync(_postId, _authorId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Post text is required.");
    }

    [Fact]
    public async Task UpdateAsync_WithTooLongText_Throws()
    {
        var command = new UpdatePostCommand(new string('x', 1001), null);
        var act = () => _service.UpdateAsync(_postId, _authorId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Post text cannot be longer than 1000 characters.");
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidMood_Throws()
    {
        var command = new UpdatePostCommand("Hello", "Raging");
        var act = () => _service.UpdateAsync(_postId, _authorId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Invalid mood value.");
    }

    [Fact]
    public async Task UpdateAsync_WhenPostNotFound_Throws()
    {
        var command = new UpdatePostCommand("Hello", null);
        var act = () => _service.UpdateAsync(Guid.NewGuid(), _authorId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Post was not found.");
    }

    [Fact]
    public async Task UpdateAsync_WhenRequesterIsNotAuthor_Throws()
    {
        var command = new UpdatePostCommand("Hello", null);
        var act = () => _service.UpdateAsync(_postId, _otherId, command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("You are not allowed to edit this post.");
    }

    [Fact]
    public async Task DeleteAsync_WhenAuthor_RemovesPost()
    {
        await _service.DeleteAsync(_postId, _authorId, CancellationToken.None);
        var post = await _dbContext.Posts.FindAsync(_postId);
        post.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenPostNotFound_Throws()
    {
        var act = () => _service.DeleteAsync(Guid.NewGuid(), _authorId, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Post was not found.");
    }

    [Fact]
    public async Task DeleteAsync_WhenRequesterIsNotAuthor_Throws()
    {
        var act = () => _service.DeleteAsync(_postId, _otherId, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("You are not allowed to delete this post.");
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
