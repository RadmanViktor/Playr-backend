using FluentAssertions;
using Playr.Domain.Games;
using Playr.Domain.Posts;

namespace Playr.Application.Tests.Games;

public class GameEntityTests
{
    [Fact]
    public void Game_has_expected_properties()
    {
        var game = new Game { Id = Guid.NewGuid(), Name = "Hollow Knight" };
        game.Name.Should().Be("Hollow Knight");
        game.CoverImageUrl.Should().BeNull();
        game.Genre.Should().BeNull();
    }

    [Fact]
    public void Post_has_expected_properties()
    {
        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            TextContent = "Cleared the Hollow Knight!",
            Mood = PostMood.Enjoying,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        post.TextContent.Should().NotBeNullOrEmpty();
        post.Mood.Should().Be(PostMood.Enjoying);
    }
}
