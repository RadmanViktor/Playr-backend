using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Friends;
using Playr.Application.Friends;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/friends")]
[Authorize]
public sealed class FriendsController(IFriendService friendService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FriendResponse>>> GetFriends(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        var friends = await friendService.GetFriendsAsync(userId, cancellationToken);
        return Ok(friends.Select(ToResponse).ToList());
    }

    [HttpGet("{userId:guid}/count")]
    public async Task<ActionResult<FriendsCountResponse>> GetFriendsCount(Guid userId, CancellationToken cancellationToken)
    {
        var count = await friendService.GetFriendsCountAsync(userId, cancellationToken);
        return Ok(new FriendsCountResponse(count));
    }

    private static FriendResponse ToResponse(FriendDto friend) => new(
        friend.UserId,
        friend.Username,
        friend.DisplayName,
        friend.AvatarUrl,
        friend.FriendsSince);
}
