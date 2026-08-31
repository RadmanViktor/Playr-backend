using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Notifications;
using Playr.Application.Notifications;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(INotificationFeedService notificationFeedService) : ControllerBase
{
    private const int DefaultTake = 20;

    [HttpGet]
    public async Task<ActionResult<NotificationFeedResponse>> GetPaged(
        [FromQuery] int skip, [FromQuery] int take, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        var effectiveTake = take <= 0 ? DefaultTake : take;
        var result = await notificationFeedService.GetPagedAsync(userId, Math.Max(skip, 0), effectiveTake, cancellationToken);
        return Ok(new NotificationFeedResponse(result.Items.Select(ToResponse).ToList(), result.HasMore, result.UnreadCount));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        try
        {
            await notificationFeedService.MarkReadAsync(userId, id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized(new { error = "User id claim is missing or invalid." });

        await notificationFeedService.MarkAllReadAsync(userId, cancellationToken);
        return NoContent();
    }

    private static NotificationResponse ToResponse(NotificationDto notification) => new(
        notification.Id,
        notification.Type,
        notification.IsRead,
        notification.CreatedAt,
        new NotificationActorResponse(
            notification.Actor.UserId,
            notification.Actor.Username,
            notification.Actor.DisplayName,
            notification.Actor.AvatarUrl),
        notification.PostId,
        notification.CommentId,
        notification.LfgGroupId);
}
