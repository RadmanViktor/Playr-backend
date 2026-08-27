using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Friends;
using Playr.Application.Friends;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/friend-requests")]
[Authorize]
public sealed class FriendRequestsController(IFriendRequestService friendRequestService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<FriendRequestResponse>> Send(SendFriendRequestRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var friendRequest = await friendRequestService.SendAsync(
                userId,
                new SendFriendRequestCommand(request.RecipientUserId),
                cancellationToken);
            return Ok(ToResponse(friendRequest));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("incoming")]
    public async Task<ActionResult<IReadOnlyList<FriendRequestResponse>>> GetIncoming(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        var requests = await friendRequestService.GetIncomingAsync(userId, cancellationToken);
        return Ok(requests.Select(ToResponse).ToList());
    }

    [HttpGet("sent")]
    public async Task<ActionResult<IReadOnlyList<FriendRequestResponse>>> GetSent(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        var requests = await friendRequestService.GetSentAsync(userId, cancellationToken);
        return Ok(requests.Select(ToResponse).ToList());
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<FriendRequestResponse>> Accept(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var request = await friendRequestService.AcceptAsync(userId, id, cancellationToken);
            return Ok(ToResponse(request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/decline")]
    public async Task<ActionResult<FriendRequestResponse>> Decline(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var request = await friendRequestService.DeclineAsync(userId, id, cancellationToken);
            return Ok(ToResponse(request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<FriendRequestResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var request = await friendRequestService.CancelAsync(userId, id, cancellationToken);
            return Ok(ToResponse(request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static FriendRequestResponse ToResponse(FriendRequestDto request) => new(
        request.Id,
        request.SenderUserId,
        request.SenderUsername,
        request.SenderDisplayName,
        request.SenderAvatarUrl,
        request.RecipientUserId,
        request.RecipientUsername,
        request.RecipientDisplayName,
        request.RecipientAvatarUrl,
        request.Status.ToString(),
        request.CreatedAt,
        request.RespondedAt);
}
