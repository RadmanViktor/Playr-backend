using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Playr.Api.Models.Auth;
using Playr.Api.Models.Games;
using Playr.Api.Models.Posts;

namespace Playr.IntegrationTests;

public sealed class HttpPostsFlowTests : IClassFixture<PlayrWebApplicationFactory>
{
    private readonly PlayrWebApplicationFactory _factory;

    public HttpPostsFlowTests(PlayrWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Can_register_login_get_games_create_post_and_read_feed()
    {
        using var client = _factory.CreateClient();

        // Register + login
        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("poster@example.com", "poster", "Password123"));
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("poster", "Password123"));
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        // GET /api/games returns a non-empty list
        var gamesResponse = await client.GetAsync("/api/games");
        gamesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var games = await gamesResponse.Content.ReadFromJsonAsync<List<GameResponse>>();
        games.Should().NotBeNullOrEmpty();
        var gameId = games![0].Id;

        // POST /api/posts creates a post
        var createResponse = await client.PostAsJsonAsync("/api/posts",
            new CreatePostRequest(gameId, "Cleared the final boss!", "Enjoying"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<PostResponse>();
        created.Should().NotBeNull();
        created!.TextContent.Should().Be("Cleared the final boss!");
        created.Mood.Should().Be("Enjoying");
        created.AuthorUsername.Should().Be("poster");

        // GET /api/posts returns the post in the feed
        var feedResponse = await client.GetAsync("/api/posts");
        feedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var feed = await feedResponse.Content.ReadFromJsonAsync<List<PostResponse>>();
        feed.Should().NotBeNullOrEmpty();
        feed!.Should().Contain(p => p.TextContent == "Cleared the final boss!");
    }

    [Fact]
    public async Task Create_post_without_auth_returns_401()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/posts",
            new CreatePostRequest(Guid.NewGuid(), "Hello!", null));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_feed_without_auth_returns_200()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/posts");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_games_without_auth_returns_200()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/games");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
