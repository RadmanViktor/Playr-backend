using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Playr.Api.Controllers;
using Playr.Application.Games;
using Playr.Application.Posts;
using Playr.Infrastructure;

namespace Playr.IntegrationTests;

public class GamesAndPostsEndpointConfigurationTests
{
    [Fact]
    public void AddInfrastructure_registers_game_and_post_services()
    {
        var services = InfrastructureTestServices.CreateWithInfrastructure();
        using var provider = services.BuildServiceProvider();
        provider.GetService<IGameService>().Should().NotBeNull();
        provider.GetService<IPostService>().Should().NotBeNull();
    }

    [Fact]
    public void Games_controller_has_correct_route_and_get_endpoint()
    {
        var apiAssembly = typeof(Program).Assembly;
        var controller = apiAssembly.GetType("Playr.Api.Controllers.GamesController");
        controller.Should().NotBeNull();
        controller!.GetCustomAttribute<ApiControllerAttribute>().Should().NotBeNull();
        controller.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/games");
        controller.GetMethods()
            .Should().Contain(m => m.GetCustomAttribute<HttpGetAttribute>() != null
                                && m.GetCustomAttribute<AuthorizeAttribute>() == null);
    }

    [Fact]
    public void Posts_controller_has_correct_route_and_endpoints()
    {
        var apiAssembly = typeof(Program).Assembly;
        var controller = apiAssembly.GetType("Playr.Api.Controllers.PostsController");
        controller.Should().NotBeNull();
        controller!.GetCustomAttribute<ApiControllerAttribute>().Should().NotBeNull();
        controller.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/posts");

        // GET /api/posts is public
        controller.GetMethods()
            .Should().Contain(m => m.GetCustomAttribute<HttpGetAttribute>() != null
                                && m.GetCustomAttribute<AuthorizeAttribute>() == null);

        // POST /api/posts requires auth
        controller.GetMethods()
            .Should().Contain(m => m.GetCustomAttribute<HttpPostAttribute>() != null
                                && m.GetCustomAttribute<AuthorizeAttribute>() != null);
    }

    [Fact]
    public async Task CreatePost_returns_unauthorized_when_user_id_claim_is_missing()
    {
        var controller = new PostsController(new ThrowingPostService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            }
        };

        var result = await controller.Create(
            new Playr.Api.Models.Posts.CreatePostRequest { GameId = Guid.NewGuid(), TextContent = "Hello!" },
            CancellationToken.None);

        var unauthorized = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.Value.Should().BeEquivalentTo(new { error = "User id claim is missing or invalid." });
    }

    [Fact]
    public void Posts_controller_has_put_and_delete_endpoints_requiring_auth()
    {
        var apiAssembly = typeof(Program).Assembly;
        var controller = apiAssembly.GetType("Playr.Api.Controllers.PostsController");
        controller.Should().NotBeNull();

        // PUT /api/posts/{id} requires auth
        controller!.GetMethods()
            .Should().Contain(m => m.GetCustomAttribute<HttpPutAttribute>() != null
                                && m.GetCustomAttribute<AuthorizeAttribute>() != null);

        // DELETE /api/posts/{id} requires auth
        controller.GetMethods()
            .Should().Contain(m => m.GetCustomAttribute<HttpDeleteAttribute>() != null
                                && m.GetCustomAttribute<AuthorizeAttribute>() != null);
    }

    [Fact]
    public async Task UpdatePost_returns_unauthorized_when_user_id_claim_is_missing()
    {
        var controller = new PostsController(new ThrowingPostService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            }
        };

        var result = await controller.Update(
            Guid.NewGuid(),
            new Playr.Api.Models.Posts.UpdatePostRequest { TextContent = "Hello" },
            CancellationToken.None);

        var unauthorized = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.Value.Should().BeEquivalentTo(new { error = "User id claim is missing or invalid." });
    }

    [Fact]
    public async Task DeletePost_returns_unauthorized_when_user_id_claim_is_missing()
    {
        var controller = new PostsController(new ThrowingPostService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            }
        };

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.Value.Should().BeEquivalentTo(new { error = "User id claim is missing or invalid." });
    }

    [Fact]
    public void Profile_posts_endpoint_is_public()
    {
        var apiAssembly = typeof(Program).Assembly;
        var controller = apiAssembly.GetType("Playr.Api.Controllers.ProfilesController");
        controller.Should().NotBeNull();
        controller!.GetMethods()
            .Should().Contain(m =>
                m.GetCustomAttribute<HttpGetAttribute>() != null &&
                m.GetCustomAttribute<HttpGetAttribute>()!.Template == "{username}/posts" &&
                m.GetCustomAttribute<AuthorizeAttribute>() == null);
    }

    private sealed class ThrowingPostService : IPostService
    {
        public Task<PostDto> CreateAsync(Guid authorId, CreatePostCommand command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Should not be called.");
        public Task<IReadOnlyList<PostDto>> GetFeedAsync(Guid? currentUserId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Should not be called.");
        public Task<PostDto?> GetByIdAsync(Guid postId, Guid? currentUserId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Should not be called.");
        public Task<PostDto> UpdateAsync(Guid postId, Guid requesterId, UpdatePostCommand command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Should not be called.");
        public Task DeleteAsync(Guid postId, Guid requesterId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Should not be called.");
        public Task<IReadOnlyList<PostDto>> GetByUsernameAsync(string username, Guid? currentUserId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Should not be called.");
        public Task<(int LikesCount, bool Liked)> ToggleLikeAsync(Guid postId, Guid userId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Should not be called.");
    }
}
