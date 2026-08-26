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

    private static Task<HttpResponseMessage> PostPostFormAsync(HttpClient client, Guid gameId, string text, string? mood)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(gameId.ToString()), "GameId" },
            { new StringContent(text), "TextContent" },
        };
        if (mood is not null) form.Add(new StringContent(mood), "Mood");
        return client.PostAsync("/api/posts", form);
    }

    private static Task<HttpResponseMessage> PutPostFormAsync(HttpClient client, Guid postId, string text, string? mood)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(text), "TextContent" },
        };
        if (mood is not null) form.Add(new StringContent(mood), "Mood");
        return client.PutAsync($"/api/posts/{postId}", form);
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
        var createResponse = await PostPostFormAsync(client, gameId, "Cleared the final boss!", "Enjoying");
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
        var response = await PostPostFormAsync(client, Guid.NewGuid(), "Hello!", null);
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

    [Fact]
    public async Task Can_edit_own_post()
    {
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("editor@example.com", "editor", "Password123"));
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("editor", "Password123"));
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var gamesResponse = await client.GetAsync("/api/games");
        var games = await gamesResponse.Content.ReadFromJsonAsync<List<GameResponse>>();
        var gameId = games![0].Id;

        var createResponse = await PostPostFormAsync(client, gameId, "Original text", null);
        var created = await createResponse.Content.ReadFromJsonAsync<PostResponse>();

        var updateResponse = await PutPostFormAsync(client, created!.Id, "Edited text", "Completed");
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<PostResponse>();
        updated!.TextContent.Should().Be("Edited text");
        updated.Mood.Should().Be("Completed");
    }

    [Fact]
    public async Task Cannot_edit_another_users_post()
    {
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("owner2@example.com", "owner2", "Password123"));
        var ownerLogin = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("owner2", "Password123"));
        var ownerToken = (await ownerLogin.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;

        var gamesResponse = await client.GetAsync("/api/games");
        var games = await gamesResponse.Content.ReadFromJsonAsync<List<GameResponse>>();
        var gameId = games![0].Id;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var createResponse = await PostPostFormAsync(client, gameId, "Owner's post", null);
        var created = await createResponse.Content.ReadFromJsonAsync<PostResponse>();

        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("intruder2@example.com", "intruder2", "Password123"));
        var intruderLogin = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("intruder2", "Password123"));
        var intruderToken = (await intruderLogin.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", intruderToken);
        var updateResponse = await PutPostFormAsync(client, created!.Id, "Hacked!", null);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Can_delete_own_post()
    {
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("deleter@example.com", "deleter", "Password123"));
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("deleter", "Password123"));
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var gamesResponse = await client.GetAsync("/api/games");
        var games = await gamesResponse.Content.ReadFromJsonAsync<List<GameResponse>>();
        var gameId = games![0].Id;

        var createResponse = await PostPostFormAsync(client, gameId, "To be deleted", null);
        var created = await createResponse.Content.ReadFromJsonAsync<PostResponse>();

        var deleteResponse = await client.DeleteAsync($"/api/posts/{created!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var feed = await client.GetAsync("/api/posts");
        var posts = await feed.Content.ReadFromJsonAsync<List<PostResponse>>();
        posts!.Should().NotContain(p => p.Id == created.Id);
    }
}
