using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Playr.Api.Models.Auth;
using Playr.Api.Models.Comments;
using Playr.Api.Models.Games;
using Playr.Api.Models.Posts;

namespace Playr.IntegrationTests;

public sealed class HttpCommentReactionsFlowTests : IClassFixture<PlayrWebApplicationFactory>
{
    private readonly PlayrWebApplicationFactory _factory;

    public HttpCommentReactionsFlowTests(PlayrWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string email, string username)
    {
        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, username, "Password123"));
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(username, "Password123"));
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        return login!.AccessToken;
    }

    private static async Task<(Guid PostId, Guid CommentId)> CreatePostAndCommentAsync(HttpClient client)
    {
        var gamesResponse = await client.GetAsync("/api/games");
        var games = await gamesResponse.Content.ReadFromJsonAsync<List<GameResponse>>();
        var gameId = games![0].Id;

        var form = new MultipartFormDataContent
        {
            { new StringContent(gameId.ToString()), "GameId" },
            { new StringContent("A post to comment on"), "TextContent" },
        };
        var createPostResponse = await client.PostAsync("/api/posts", form);
        var post = await createPostResponse.Content.ReadFromJsonAsync<PostResponse>();

        var createCommentResponse = await client.PostAsJsonAsync(
            $"/api/posts/{post!.Id}/comments", new CreateCommentRequest("Nice post!"));
        var comment = await createCommentResponse.Content.ReadFromJsonAsync<CommentResponse>();

        return (post.Id, comment!.Id);
    }

    [Fact]
    public async Task SetReaction_then_GetPaged_reflects_count_and_current_user_reaction()
    {
        using var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "reactor1@example.com", "reactor1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var (postId, commentId) = await CreatePostAndCommentAsync(client);

        var reactResponse = await client.PutAsJsonAsync(
            $"/api/posts/{postId}/comments/{commentId}/reactions", new SetReactionRequest("Like"));
        reactResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reaction = await reactResponse.Content.ReadFromJsonAsync<CommentReactionResponse>();
        reaction!.Counts.Like.Should().Be(1);
        reaction.CurrentUserReaction.Should().Be("Like");

        var pagedResponse = await client.GetAsync($"/api/posts/{postId}/comments");
        pagedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await pagedResponse.Content.ReadFromJsonAsync<PagedCommentResponse>();
        var listedComment = paged!.Items.Single(c => c.Id == commentId);
        listedComment.Reactions.Counts.Like.Should().Be(1);
        listedComment.Reactions.CurrentUserReaction.Should().Be("Like");
    }

    [Fact]
    public async Task SetReaction_twice_with_same_type_toggles_it_off()
    {
        using var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "reactor2@example.com", "reactor2");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var (postId, commentId) = await CreatePostAndCommentAsync(client);

        await client.PutAsJsonAsync($"/api/posts/{postId}/comments/{commentId}/reactions", new SetReactionRequest("Like"));
        var secondResponse = await client.PutAsJsonAsync(
            $"/api/posts/{postId}/comments/{commentId}/reactions", new SetReactionRequest("Like"));

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reaction = await secondResponse.Content.ReadFromJsonAsync<CommentReactionResponse>();
        reaction!.Counts.Like.Should().Be(0);
        reaction.CurrentUserReaction.Should().BeNull();
    }

    [Fact]
    public async Task SetReaction_without_authentication_returns_401()
    {
        using var client = _factory.CreateClient();
        var response = await client.PutAsJsonAsync(
            $"/api/posts/{Guid.NewGuid()}/comments/{Guid.NewGuid()}/reactions", new SetReactionRequest("Like"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetReaction_on_unknown_comment_returns_404()
    {
        using var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "reactor3@example.com", "reactor3");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var (postId, _) = await CreatePostAndCommentAsync(client);

        var response = await client.PutAsJsonAsync(
            $"/api/posts/{postId}/comments/{Guid.NewGuid()}/reactions", new SetReactionRequest("Like"));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetReaction_with_invalid_type_returns_400()
    {
        using var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "reactor4@example.com", "reactor4");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var (postId, commentId) = await CreatePostAndCommentAsync(client);

        var response = await client.PutAsJsonAsync(
            $"/api/posts/{postId}/comments/{commentId}/reactions", new SetReactionRequest("Excited"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteReaction_removes_existing_reaction()
    {
        using var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "reactor5@example.com", "reactor5");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var (postId, commentId) = await CreatePostAndCommentAsync(client);

        await client.PutAsJsonAsync($"/api/posts/{postId}/comments/{commentId}/reactions", new SetReactionRequest("Angry"));
        var deleteResponse = await client.DeleteAsync($"/api/posts/{postId}/comments/{commentId}/reactions");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reaction = await deleteResponse.Content.ReadFromJsonAsync<CommentReactionResponse>();
        reaction!.Counts.Angry.Should().Be(0);
        reaction.CurrentUserReaction.Should().BeNull();
    }
}
