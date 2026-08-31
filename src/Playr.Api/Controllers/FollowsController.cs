using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Follows;
using Playr.Application.Follows;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/follows")]
[Authorize]
public sealed class FollowsController(IFollowService followService) : ControllerBase
{
    [HttpPost("{userId:guid}")]
    public async Task<ActionResult<FollowResponse>> Follow(Guid userId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var currentUserId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var follow = await followService.FollowAsync(currentUserId, userId, cancellationToken);
            return Ok(ToResponse(follow));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Unfollow(Guid userId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var currentUserId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        await followService.UnfollowAsync(currentUserId, userId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{userId:guid}/status")]
    public async Task<ActionResult<FollowStatusResponse>> GetStatus(Guid userId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var currentUserId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        var isFollowing = await followService.IsFollowingAsync(currentUserId, userId, cancellationToken);
        return Ok(new FollowStatusResponse(isFollowing));
    }

    [HttpGet("{userId:guid}/counts")]
    public async Task<ActionResult<FollowCountsResponse>> GetCounts(Guid userId, CancellationToken cancellationToken)
    {
        var counts = await followService.GetCountsAsync(userId, cancellationToken);
        return Ok(new FollowCountsResponse(counts.FollowersCount, counts.FollowingCount));
    }

    [HttpGet("{userId:guid}/followers")]
    public async Task<ActionResult<IReadOnlyList<FollowResponse>>> GetFollowers(Guid userId, CancellationToken cancellationToken)
    {
        var followers = await followService.GetFollowersAsync(userId, cancellationToken);
        return Ok(followers.Select(ToResponse).ToList());
    }

    [HttpGet("{userId:guid}/following")]
    public async Task<ActionResult<IReadOnlyList<FollowResponse>>> GetFollowing(Guid userId, CancellationToken cancellationToken)
    {
        var following = await followService.GetFollowingAsync(userId, cancellationToken);
        return Ok(following.Select(ToResponse).ToList());
    }

    private static FollowResponse ToResponse(FollowDto follow) => new(
        follow.UserId,
        follow.Username,
        follow.DisplayName,
        follow.AvatarUrl,
        follow.FollowingSince);
}
