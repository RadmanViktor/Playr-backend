using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Lfg;
using Playr.Application.Lfg;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/lfg-groups")]
[Authorize]
public sealed class LfgGroupsController(ILfgGroupService lfgGroupService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<LfgGroupResponse>> Create(CreateLfgGroupRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var group = await lfgGroupService.CreateGroupAsync(
                userId,
                new CreateLfgGroupCommand(
                    request.GameId,
                    request.PlayersWanted,
                    request.PlayStyle,
                    request.Note,
                    request.PreferredMinAge,
                    request.PreferredMaxAge,
                    request.MicrophoneRequired),
                cancellationToken);
            return Ok(ToResponse(group));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("open")]
    public async Task<ActionResult<IReadOnlyList<LfgGroupResponse>>> GetOpen(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        var groups = await lfgGroupService.GetOpenGroupsAsync(userId, cancellationToken);
        return Ok(groups.Select(ToResponse).ToList());
    }

    [HttpPost("{id:guid}/apply")]
    public async Task<ActionResult<LfgGroupApplicationResponse>> Apply(Guid id, ApplyToLfgGroupRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var application = await lfgGroupService.ApplyAsync(userId, id, request.Message, cancellationToken);
            return Ok(ToResponse(application));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("incoming-applications")]
    public async Task<ActionResult<IReadOnlyList<LfgGroupApplicationResponse>>> GetIncomingApplications(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        var applications = await lfgGroupService.GetIncomingApplicationsAsync(userId, cancellationToken);
        return Ok(applications.Select(ToResponse).ToList());
    }

    [HttpPost("applications/{applicationId:guid}/accept")]
    public async Task<ActionResult<LfgGroupApplicationResponse>> AcceptApplication(Guid applicationId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var application = await lfgGroupService.AcceptApplicationAsync(userId, applicationId, cancellationToken);
            return Ok(ToResponse(application));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("applications/{applicationId:guid}/decline")]
    public async Task<ActionResult<LfgGroupApplicationResponse>> DeclineApplication(Guid applicationId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var application = await lfgGroupService.DeclineApplicationAsync(userId, applicationId, cancellationToken);
            return Ok(ToResponse(application));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/invite")]
    public async Task<ActionResult<LfgGroupInviteResponse>> Invite(Guid id, InviteToLfgGroupRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var invite = await lfgGroupService.InviteToGroupAsync(userId, id, request.InviteeUserId, cancellationToken);
            return Ok(ToResponse(invite));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("my-invites")]
    public async Task<ActionResult<IReadOnlyList<LfgGroupInviteResponse>>> GetMyInvites(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        var invites = await lfgGroupService.GetMyGroupInvitesAsync(userId, cancellationToken);
        return Ok(invites.Select(ToResponse).ToList());
    }

    [HttpPost("invites/{inviteId:guid}/accept")]
    public async Task<ActionResult<LfgGroupInviteResponse>> AcceptInvite(Guid inviteId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var invite = await lfgGroupService.RespondToGroupInviteAsync(userId, inviteId, true, cancellationToken);
            return Ok(ToResponse(invite));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("invites/{inviteId:guid}/decline")]
    public async Task<ActionResult<LfgGroupInviteResponse>> DeclineInvite(Guid inviteId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var invite = await lfgGroupService.RespondToGroupInviteAsync(userId, inviteId, false, cancellationToken);
            return Ok(ToResponse(invite));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<LfgGroupResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var group = await lfgGroupService.CancelGroupAsync(userId, id, cancellationToken);
            return Ok(ToResponse(group));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static LfgGroupResponse ToResponse(LfgGroupDto group) => new(
        group.Id,
        group.CreatorUserId,
        group.CreatorUsername,
        group.CreatorDisplayName,
        group.CreatorAvatarUrl,
        group.GameId,
        group.GameName,
        group.GameCoverImageUrl,
        group.PlayStyle,
        group.Note,
        group.PlayersWanted,
        group.AcceptedCount,
        group.Status.ToString(),
        group.CreatedAt,
        group.FilledAt,
        group.CancelledAt,
        group.MyMembershipStatus.ToString(),
        group.MyApplicationStatus?.ToString(),
        group.MyInviteStatus?.ToString(),
        group.PreferredMinAge,
        group.PreferredMaxAge,
        group.MicrophoneRequired);

    private static LfgGroupApplicationResponse ToResponse(LfgGroupApplicationDto application) => new(
        application.Id,
        application.LfgGroupId,
        application.GameName,
        application.ApplicantUserId,
        application.ApplicantUsername,
        application.ApplicantDisplayName,
        application.ApplicantAvatarUrl,
        application.Status.ToString(),
        application.Message,
        application.CreatedAt,
        application.RespondedAt);

    private static LfgGroupInviteResponse ToResponse(LfgGroupInviteDto invite) => new(
        invite.Id,
        invite.LfgGroupId,
        invite.GameName,
        invite.InviterUserId,
        invite.InviteeUserId,
        invite.InviteeUsername,
        invite.InviteeDisplayName,
        invite.InviteeAvatarUrl,
        invite.Status.ToString(),
        invite.CreatedAt,
        invite.RespondedAt);
}
