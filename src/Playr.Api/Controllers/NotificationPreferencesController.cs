using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Notifications;
using Playr.Application.Notifications;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/notification-preferences")]
[Authorize]
public sealed class NotificationPreferencesController(INotificationPreferencesService notificationPreferencesService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationPreferencesResponse>> Get(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var preferences = await notificationPreferencesService.GetAsync(userId, cancellationToken);
            return Ok(ToResponse(preferences));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut]
    public async Task<ActionResult<NotificationPreferencesResponse>> Update(
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var preferences = await notificationPreferencesService.UpdateAsync(
                userId,
                new UpdateNotificationPreferencesCommand(request.ChatSoundEnabled, request.ChatBrowserNotificationsEnabled),
                cancellationToken);
            return Ok(ToResponse(preferences));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private static NotificationPreferencesResponse ToResponse(NotificationPreferencesDto dto) =>
        new(dto.ChatSoundEnabled, dto.ChatBrowserNotificationsEnabled);
}
