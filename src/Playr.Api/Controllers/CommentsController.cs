using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Comments;
using Playr.Api.Models.Common;
using Playr.Application.Comments;
using Playr.Domain.Comments;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/posts/{postId:guid}/comments")]
public sealed class CommentsController(ICommentService commentService) : ControllerBase
{
    private const int DefaultTake = 20;
    private const int MaxTake = 50;

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<CommentResponse>> Create(Guid postId, CreateCommentRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        try
        {
            var comment = await commentService.CreateAsync(postId, userId, new CreateCommentCommand(request.TextContent, request.MentionedUserIds), cancellationToken);
            return CreatedAtAction(nameof(GetPaged), new { postId }, ToResponse(comment));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Post was not found.")
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<PagedCommentResponse>> GetPaged(Guid postId, [FromQuery] int skip, [FromQuery] int take, CancellationToken cancellationToken)
    {
        var effectiveTake = take <= 0 ? DefaultTake : Math.Min(take, MaxTake);
        var effectiveSkip = Math.Max(skip, 0);
        Guid? currentUserId = User.TryGetUserId(out var userId) ? userId : null;

        try
        {
            var result = await commentService.GetPagedAsync(postId, currentUserId, effectiveSkip, effectiveTake, cancellationToken);
            return Ok(new PagedCommentResponse(result.Items.Select(ToResponse).ToList(), result.TotalCount, result.HasMore));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Post was not found.")
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("{commentId:guid}")]
    public async Task<ActionResult<CommentResponse>> Update(Guid postId, Guid commentId, UpdateCommentRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        try
        {
            var comment = await commentService.UpdateAsync(postId, commentId, userId, new UpdateCommentCommand(request.TextContent), cancellationToken);
            return Ok(ToResponse(comment));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Comment was not found.")
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
    [HttpDelete("{commentId:guid}")]
    public async Task<IActionResult> Delete(Guid postId, Guid commentId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        try
        {
            await commentService.DeleteAsync(postId, commentId, userId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "Comment was not found.")
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("You are not allowed to"))
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("{commentId:guid}/reactions")]
    public async Task<ActionResult<CommentReactionResponse>> SetReaction(Guid postId, Guid commentId, SetReactionRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        if (!Enum.TryParse<ReactionType>(request.Type, ignoreCase: true, out var type))
        {
            var validValues = string.Join(", ", Enum.GetNames<ReactionType>());
            return BadRequest(new { error = $"Invalid reaction type. Valid values are: {validValues}." });
        }

        try
        {
            var summary = await commentService.SetReactionAsync(postId, commentId, userId, type, cancellationToken);
            return Ok(ToResponse(summary));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Comment was not found.")
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpDelete("{commentId:guid}/reactions")]
    public async Task<ActionResult<CommentReactionResponse>> RemoveReaction(Guid postId, Guid commentId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        try
        {
            var summary = await commentService.RemoveReactionAsync(postId, commentId, userId, cancellationToken);
            return Ok(ToResponse(summary));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Comment was not found.")
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private static CommentResponse ToResponse(CommentDto comment) => new(
        comment.Id,
        comment.PostId,
        comment.AuthorId,
        comment.AuthorUsername,
        comment.AuthorDisplayName,
        comment.AuthorAvatarUrl,
        comment.TextContent,
        comment.CreatedAt,
        comment.UpdatedAt,
        ToResponse(comment.Reactions),
        comment.Mentions.Select(m => new MentionResponse(m.UserId, m.Username, m.DisplayName)).ToList());

    private static CommentReactionResponse ToResponse(CommentReactionSummary summary) => new(
        new ReactionCountsResponse(summary.Counts.Like, summary.Counts.Haha, summary.Counts.Wow, summary.Counts.Sad, summary.Counts.Angry),
        summary.CurrentUserReaction?.ToString());
}
