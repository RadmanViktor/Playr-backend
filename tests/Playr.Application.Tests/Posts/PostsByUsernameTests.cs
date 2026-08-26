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

public sealed class PostsByUsernameTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PlayrDbContext _dbContext;
    private readonly PostService _service;
    private readonly Guid _userId;
    private readonly Guid _gameId;

    public PostsByUsernameTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<PlayrDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new PlayrDbContext(options);
        _dbContext.Database.EnsureCreated();

        _userId = Guid.NewGuid();
        _gameId = Guid.NewGuid();

        _dbContext.Users.Add(new ApplicationUser
        {
            Id = _userId, Email = "gamer@example.com", UserName = "gamer",
            NormalizedEmail = "GAMER@EXAMPLE.COM", NormalizedUserName = "GAMER",
        });
        _dbContext.UserProfiles.Add(new UserProfile
        {
            UserId = _userId, Username = "gamer", DisplayName = "Gamer",
        });
        _dbContext.Games.Add(new Game { Id = _gameId, Name = "Hollow Knight" });
        _dbContext.Posts.AddRange(
            new Post { Id = Guid.NewGuid(), AuthorId = _userId, GameId = _gameId, TextContent = "Post A", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2) },
            new Post { Id = Guid.NewGuid(), AuthorId = _userId, GameId = _gameId, TextContent = "Post B", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1) }
        );
        _dbContext.SaveChanges();
        _service = new PostService(_dbContext, new NoOpFileStorageService());
    }

    [Fact]
    public async Task GetByUsernameAsync_ReturnsPostsNewestFirst()
    {
        var result = await _service.GetByUsernameAsync("gamer", null, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].TextContent.Should().Be("Post B");
        result[1].TextContent.Should().Be("Post A");
    }

    [Fact]
    public async Task GetByUsernameAsync_IsCaseInsensitive()
    {
        var result = await _service.GetByUsernameAsync("GAMER", null, CancellationToken.None);
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByUsernameAsync_ReturnsEmptyListForUnknownUsername()
    {
        var result = await _service.GetByUsernameAsync("nobody", null, CancellationToken.None);
        result.Should().BeEmpty();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
