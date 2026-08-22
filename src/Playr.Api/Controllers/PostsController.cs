using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Posts;
using Playr.Application.Posts;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/posts")]
public sealed class PostsController(IPostService postService) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<PostResponse>> Create(CreatePostRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        try
        {
            var post = await postService.CreateAsync(userId,
                new CreatePostCommand(request.GameId, request.TextContent, request.Mood),
                cancellationToken);
            return CreatedAtAction(nameof(GetFeed), ToResponse(post));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PostResponse>>> GetFeed(CancellationToken cancellationToken)
    {
        var posts = await postService.GetFeedAsync(cancellationToken);
        return Ok(posts.Select(ToResponse).ToList());
    }

    private static PostResponse ToResponse(PostDto post) => new(
        post.Id,
        post.AuthorId,
        post.AuthorUsername,
        post.AuthorDisplayName,
        post.AuthorAvatarUrl,
        post.GameId,
        post.GameName,
        post.GameCoverImageUrl,
        post.TextContent,
        post.Mood,
        post.CreatedAt);
}
