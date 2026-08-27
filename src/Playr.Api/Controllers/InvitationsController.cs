using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Invitations;
using Playr.Application.Invitations;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/invitations")]
[Authorize]
public sealed class InvitationsController(IInvitationService invitationService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<InvitationResponse>> Send(SendInvitationRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var invitation = await invitationService.SendAsync(
                userId,
                new SendInvitationCommand(request.RecipientUserId, request.Message),
                cancellationToken);
            return Ok(ToResponse(invitation));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("incoming")]
    public async Task<ActionResult<IReadOnlyList<InvitationResponse>>> GetIncoming(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        var invitations = await invitationService.GetIncomingAsync(userId, cancellationToken);
        return Ok(invitations.Select(ToResponse).ToList());
    }

    [HttpGet("sent")]
    public async Task<ActionResult<IReadOnlyList<InvitationResponse>>> GetSent(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        var invitations = await invitationService.GetSentAsync(userId, cancellationToken);
        return Ok(invitations.Select(ToResponse).ToList());
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<InvitationResponse>> Accept(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var invitation = await invitationService.AcceptAsync(userId, id, cancellationToken);
            return Ok(ToResponse(invitation));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/decline")]
    public async Task<ActionResult<InvitationResponse>> Decline(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var invitation = await invitationService.DeclineAsync(userId, id, cancellationToken);
            return Ok(ToResponse(invitation));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<InvitationResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var invitation = await invitationService.CancelAsync(userId, id, cancellationToken);
            return Ok(ToResponse(invitation));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static InvitationResponse ToResponse(InvitationDto invitation) => new(
        invitation.Id,
        invitation.SenderUserId,
        invitation.SenderUsername,
        invitation.SenderDisplayName,
        invitation.SenderAvatarUrl,
        invitation.RecipientUserId,
        invitation.RecipientUsername,
        invitation.RecipientDisplayName,
        invitation.RecipientAvatarUrl,
        invitation.Message,
        invitation.Status.ToString(),
        invitation.CreatedAt,
        invitation.RespondedAt);
}
