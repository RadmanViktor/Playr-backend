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
    public async Task<ActionResult<PostResponse>> Create([FromForm] CreatePostRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        try
        {
            var post = await postService.CreateAsync(userId,
                new CreatePostCommand(request.GameId, request.TextContent, request.Mood, ToMediaInput(request.Media)),
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
        Guid? currentUserId = User.TryGetUserId(out var uid) ? uid : null;
        var posts = await postService.GetFeedAsync(currentUserId, cancellationToken);
        return Ok(posts.Select(ToResponse).ToList());
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PostResponse>> Update(Guid id, [FromForm] UpdatePostRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        try
        {
            var post = await postService.UpdateAsync(id, userId,
                new UpdatePostCommand(request.TextContent, request.Mood, ToMediaInput(request.Media), request.RemoveMedia),
                cancellationToken);
            return Ok(ToResponse(post));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Post was not found.")
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("You are not allowed to"))
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        try
        {
            await postService.DeleteAsync(id, userId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "Post was not found.")
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("You are not allowed to"))
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    private static PostMediaInput? ToMediaInput(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return null;

        return new PostMediaInput(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
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
        post.MediaUrl,
        post.MediaType,
        post.CreatedAt,
        post.LikesCount,
        post.LikedByCurrentUser);

    [Authorize]
    [HttpPost("{id:guid}/like")]
    public async Task<ActionResult<object>> ToggleLike(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        try
        {
            var (likesCount, liked) = await postService.ToggleLikeAsync(id, userId, cancellationToken);
            return Ok(new { likesCount, liked });
        }
        catch (InvalidOperationException ex) when (ex.Message == "Post was not found.")
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
